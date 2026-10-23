package com.solera.formgen;

import java.io.*;
import java.lang.instrument.Instrumentation;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;

public class FormGenAgent {
    public static volatile Object formGenInstance = null;
    public static volatile Object mainShell = null;
    private static volatile boolean serverStarted = false;
    private static final String PORT_FILE = System.getProperty("java.io.tmpdir") + File.separator + "formgen_agent.port";
    private static final String LOG_FILE = System.getProperty("java.io.tmpdir") + File.separator + "formgen_agent.log";

    public static void log(String msg) {
        try {
            FileWriter fw = new FileWriter(LOG_FILE, true);
            fw.write(new java.util.Date() + " " + msg + "\n");
            fw.close();
        } catch (Throwable ignored) {}
        System.out.println("[FormGenAgent] " + msg);
    }

    public static void premain(String agentArgs, Instrumentation inst) {
        init(agentArgs, false);
    }

    public static void agentmain(String agentArgs, Instrumentation inst) {
        init(agentArgs, true);
    }

    private static synchronized void init(final String agentArgs, boolean isDynamicAttach) {
        log("Initializing agent (args: " + agentArgs + ", dynamic: " + isDynamicAttach + ")...");

        String sysPropFile = System.getProperty("formgen.file");
        String fileToOpen = cleanFilePath(sysPropFile != null && !sysPropFile.trim().isEmpty() ? sysPropFile : agentArgs);

        // If starting as a second process, forward to the running instance and exit
        if (!isDynamicAttach && tryForwardToExistingInstance(fileToOpen)) {
            log("Forwarded to existing instance. Exiting secondary JVM.");
            System.exit(0);
            return;
        }

        startIpcServer();

        // Register shutdown hook to delete port file on exit
        try {
            Runtime.getRuntime().addShutdownHook(new Thread(new Runnable() {
                @Override
                public void run() {
                    try {
                        new File(PORT_FILE).delete();
                    } catch (Throwable ignored) {}
                }
            }));
        } catch (Throwable ignored) {}

        if (!fileToOpen.isEmpty()) {
            openFileWithRetry(fileToOpen, 120); // retry up to 60s
        }
    }

    private static String cleanFilePath(String raw) {
        if (raw == null) return "";
        String s = raw.trim();
        while (s.startsWith("\uFEFF")) {
            s = s.substring(1).trim();
        }
        if (s.startsWith("\"") && s.endsWith("\"") && s.length() >= 2) {
            s = s.substring(1, s.length() - 1).trim();
        }
        while (s.startsWith("\uFEFF")) {
            s = s.substring(1).trim();
        }
        return s;
    }

    private static boolean tryForwardToExistingInstance(String filePath) {
        try {
            File portFile = new File(PORT_FILE);
            if (!portFile.exists()) return false;

            BufferedReader br = new BufferedReader(new FileReader(portFile));
            String line = br.readLine();
            br.close();

            if (line == null || line.trim().isEmpty()) return false;
            int port = Integer.parseInt(line.trim());

            Socket socket = new Socket(InetAddress.getByName("127.0.0.1"), port);
            socket.setSoTimeout(2000);

            PrintWriter out = new PrintWriter(new OutputStreamWriter(socket.getOutputStream(), "UTF-8"), true);
            BufferedReader in = new BufferedReader(new InputStreamReader(socket.getInputStream(), "UTF-8"));

            String message = (filePath != null && !filePath.isEmpty()) ? filePath : "STATUS";
            out.println(message);
            String response = in.readLine();
            socket.close();

            log("IPC Forward to port " + port + " returned: " + response);
            return response != null && !response.trim().isEmpty();
        } catch (Throwable t) {
            log("Existing instance not reachable: " + t.getMessage());
            try { new File(PORT_FILE).delete(); } catch (Throwable ignored) {}
            return false;
        }
    }

    private static void openFileWithRetry(final String filePath, final int retriesLeft) {
        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    Object display = null;
                    for (int i = 0; i < 40; i++) {
                        display = getDisplay();
                        if (display != null && !isDisposed(display)) break;
                        Thread.sleep(250);
                    }

                    if (display == null) {
                        if (retriesLeft > 0) {
                            Thread.sleep(500);
                            openFileWithRetry(filePath, retriesLeft - 1);
                        }
                        return;
                    }

                    runOnDisplay(display, new Runnable() {
                        @Override
                        public void run() {
                            if (discoverFormGen()) {
                                openFile(filePath);
                            } else if (retriesLeft > 0) {
                                Object d = getDisplay();
                                if (d != null && !isDisposed(d)) {
                                    runTimer(d, 500, new Runnable() {
                                        @Override
                                        public void run() {
                                            openFileWithRetry(filePath, retriesLeft - 1);
                                        }
                                    });
                                }
                            }
                        }
                    });
                } catch (Throwable t) {
                    log("Error in openFileWithRetry: " + t.getMessage());
                }
            }
        }, "FormGenAgent-StartupOpen").start();
    }

    private static Object getDisplay() {
        try {
            Class<?> displayClass = Class.forName("org.eclipse.swt.widgets.Display");
            return displayClass.getMethod("getDefault").invoke(null);
        } catch (Throwable t) {
            return null;
        }
    }

    private static boolean isDisposed(Object swtObj) {
        if (swtObj == null) return true;
        try {
            Method m = swtObj.getClass().getMethod("isDisposed");
            return (Boolean) m.invoke(swtObj);
        } catch (Throwable t) {
            return true;
        }
    }

    private static void runOnDisplay(Object display, Runnable runnable) {
        if (display == null) return;
        try {
            Method m = display.getClass().getMethod("asyncExec", Runnable.class);
            m.invoke(display, runnable);
        } catch (Throwable t) {
            log("Error invoking asyncExec: " + t.getMessage());
        }
    }

    private static void runTimer(Object display, int delayMs, Runnable runnable) {
        if (display == null) return;
        try {
            Method m = display.getClass().getMethod("timerExec", int.class, Runnable.class);
            m.invoke(display, delayMs, runnable);
        } catch (Throwable t) {
            log("Error invoking timerExec: " + t.getMessage());
        }
    }

    public static boolean discoverFormGen() {
        if (formGenInstance != null && mainShell != null && !isDisposed(mainShell)) {
            return true;
        }

        Object display = getDisplay();
        if (display == null || isDisposed(display)) return false;

        try {
            Method getShellsMethod = display.getClass().getMethod("getShells");
            Object[] shells = (Object[]) getShellsMethod.invoke(display);
            if (shells != null) {
                for (Object s : shells) {
                    Object nfg = findNewFormGen(s);
                    if (nfg != null) {
                        formGenInstance = nfg;
                        mainShell = s;
                        log("Successfully linked to NewFormGen: " + nfg.getClass().getName());
                        return true;
                    }
                }
            }
        } catch (Throwable t) {
            log("Error in discoverFormGen: " + t.getMessage());
        }
        return false;
    }

    private static Object findNewFormGen(Object widget) {
        if (widget == null || isDisposed(widget)) return null;

        try {
            Class<?> widgetClass = Class.forName("org.eclipse.swt.widgets.Widget");
            Field eventTableField = widgetClass.getDeclaredField("eventTable");
            eventTableField.setAccessible(true);
            Object eventTable = eventTableField.get(widget);
            if (eventTable != null) {
                Field listenersField = eventTable.getClass().getDeclaredField("listeners");
                listenersField.setAccessible(true);
                Object[] listeners = (Object[]) listenersField.get(eventTable);
                if (listeners != null) {
                    for (Object l : listeners) {
                        if (l != null) {
                            Object nfg = extractFromThis0(l);
                            if (nfg != null) return nfg;
                        }
                    }
                }
            }
        } catch (Throwable ignored) {}

        try {
            Class<?> compositeClass = Class.forName("org.eclipse.swt.widgets.Composite");
            if (compositeClass.isInstance(widget)) {
                Method getChildrenMethod = compositeClass.getMethod("getChildren");
                Object[] children = (Object[]) getChildrenMethod.invoke(widget);
                if (children != null) {
                    for (Object c : children) {
                        Object nfg = findNewFormGen(c);
                        if (nfg != null) return nfg;
                    }
                }
            }
        } catch (Throwable ignored) {}

        return null;
    }

    private static Object extractFromThis0(Object obj) {
        if (obj == null) return null;
        Class<?> cls = obj.getClass();
        while (cls != null && cls != Object.class) {
            for (Field f : cls.getDeclaredFields()) {
                if (f.getName().startsWith("this$") && f.getType().getName().equals("com.automate.formgen.NewFormGen")) {
                    try {
                        f.setAccessible(true);
                        return f.get(obj);
                    } catch (Throwable ignored) {}
                }
            }
            cls = cls.getSuperclass();
        }
        return null;
    }

    public static String getStatus() {
        if (formGenInstance == null) return "DISCONNECTED";
        final String[] result = new String[] { "DISCONNECTED" };
        Object display = getDisplay();
        if (display != null && !isDisposed(display)) {
            try {
                Method syncExecMethod = display.getClass().getMethod("syncExec", Runnable.class);
                syncExecMethod.invoke(display, new Runnable() {
                    @Override
                    public void run() {
                        try {
                            Method m = formGenInstance.getClass().getDeclaredMethod("hasActiveFile");
                            m.setAccessible(true);
                            boolean hasActive = (Boolean) m.invoke(formGenInstance);
                            String shellText = "null";
                            if (mainShell != null && !isDisposed(mainShell)) {
                                try {
                                    shellText = (String) mainShell.getClass().getMethod("getText").invoke(mainShell);
                                } catch (Throwable ignored) {}
                            }
                            result[0] = "CONNECTED: hasActiveFile=" + hasActive + " ShellText=" + shellText;
                        } catch (Throwable t) {
                            result[0] = "ERROR: " + t.getMessage();
                        }
                    }
                });
            } catch (Throwable t) {
                return "ERROR: " + t.getMessage();
            }
        }
        return result[0];
    }

    public static void openFile(final String rawPath) {
        Object display = getDisplay();
        if (display == null || isDisposed(display)) {
            log("Display is null or disposed when opening: " + rawPath);
            return;
        }

        runOnDisplay(display, new Runnable() {
            @Override
            public void run() {
                try {
                    if (!discoverFormGen()) {
                        log("Could not discover NewFormGen instance for: " + rawPath);
                        return;
                    }

                    String cleanPath = cleanFilePath(rawPath);
                    File file = new File(cleanPath);
                    if (!file.exists()) {
                        log("File does not exist: " + cleanPath);
                        return;
                    }

                    log("Invoking openFormgenFile on: " + file.getAbsolutePath());
                    Method openMethod = formGenInstance.getClass().getDeclaredMethod("openFormgenFile", File.class, boolean.class);
                    openMethod.setAccessible(true);
                    Object result = openMethod.invoke(formGenInstance, file, false);
                    log("openFormgenFile returned: " + result);

                    if (mainShell != null && !isDisposed(mainShell)) {
                        try {
                            mainShell.getClass().getMethod("setMinimized", boolean.class).invoke(mainShell, false);
                            mainShell.getClass().getMethod("setActive").invoke(mainShell);
                            mainShell.getClass().getMethod("forceActive").invoke(mainShell);
                        } catch (Throwable ignored) {}
                    }
                } catch (Throwable t) {
                    StringWriter sw = new StringWriter();
                    t.printStackTrace(new PrintWriter(sw));
                    log("Error invoking openFormgenFile: " + sw.toString());
                }
            }
        });
    }

    private static synchronized void startIpcServer() {
        if (serverStarted) return;
        serverStarted = true;

        Thread serverThread = new Thread(new Runnable() {
            @Override
            public void run() {
                ServerSocket serverSocket = null;
                try {
                    serverSocket = new ServerSocket(0, 50, InetAddress.getByName("127.0.0.1"));
                    int port = serverSocket.getLocalPort();
                    log("IPC Server listening on 127.0.0.1:" + port);

                    FileWriter fw = new FileWriter(PORT_FILE);
                    fw.write(String.valueOf(port));
                    fw.close();

                    while (!serverSocket.isClosed()) {
                        final Socket socket = serverSocket.accept();
                        new Thread(new Runnable() {
                            @Override
                            public void run() {
                                try {
                                    BufferedReader br = new BufferedReader(new InputStreamReader(socket.getInputStream(), "UTF-8"));
                                    String line = br.readLine();
                                    if (line != null) {
                                        line = cleanFilePath(line);
                                        if (line.equalsIgnoreCase("STATUS")) {
                                            String st = getStatus();
                                            OutputStream out = socket.getOutputStream();
                                            out.write((st + "\n").getBytes("UTF-8"));
                                            out.flush();
                                            activateMainShell();
                                        } else if (line.startsWith("OPEN:")) {
                                            String target = line.substring(5).trim();
                                            openFile(target);
                                            OutputStream out = socket.getOutputStream();
                                            out.write("OK\n".getBytes("UTF-8"));
                                            out.flush();
                                        } else if (!line.isEmpty()) {
                                            openFile(line);
                                            OutputStream out = socket.getOutputStream();
                                            out.write("OK\n".getBytes("UTF-8"));
                                            out.flush();
                                        }
                                    }
                                    socket.close();
                                } catch (Throwable ignored) {}
                            }
                        }).start();
                    }
                } catch (Throwable t) {
                    log("IPC Server error: " + t.getMessage());
                }
            }
        }, "FormGenAgent-IPC");
        serverThread.setDaemon(true);
        serverThread.start();
    }

    private static void activateMainShell() {
        if (mainShell == null || isDisposed(mainShell)) return;
        Object display = getDisplay();
        if (display != null && !isDisposed(display)) {
            runOnDisplay(display, new Runnable() {
                @Override
                public void run() {
                    try {
                        mainShell.getClass().getMethod("setMinimized", boolean.class).invoke(mainShell, false);
                        mainShell.getClass().getMethod("setActive").invoke(mainShell);
                        mainShell.getClass().getMethod("forceActive").invoke(mainShell);
                    } catch (Throwable ignored) {}
                }
            });
        }
    }
}
