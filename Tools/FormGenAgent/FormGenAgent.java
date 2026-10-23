package com.solera.formgen;

import java.io.*;
import java.lang.instrument.Instrumentation;
import java.lang.reflect.*;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.util.*;
import java.util.regex.*;

public class FormGenAgent {
    public static volatile Object formGenInstance = null;
    public static volatile Object mainShell = null;
    public static volatile String scmtPath = null;
    public static volatile String lastOpenedFilePath = null;
    public static final Map<String, String> liveVariables = new LinkedHashMap<String, String>();

    private static volatile boolean serverStarted = false;
    private static volatile boolean menuHookInstalled = false;
    private static final String PORT_FILE = System.getProperty("java.io.tmpdir") + File.separator + "formgen_agent.port";
    private static final String LOG_FILE = System.getProperty("java.io.tmpdir") + File.separator + "formgen_agent.log";

    // SWT Constants
    private static final int SWT_KeyDown = 1;
    private static final int SWT_SEPARATOR = 2;
    private static final int SWT_POP_UP = 8;
    private static final int SWT_PUSH = 8;
    private static final int SWT_Selection = 13;
    private static final int SWT_FocusIn = 15;
    private static final int SWT_Show = 22;
    private static final int SWT_Activate = 26;
    private static final int SWT_MenuDetect = 35;
    private static final int SWT_CASCADE = 64;
    private static final int SWT_MOD1 = 262144;  // SWT.CONTROL
    private static final int SWT_SHIFT = 131072; // SWT.SHIFT

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

        if (!isDynamicAttach && tryForwardToExistingInstance(fileToOpen)) {
            log("Forwarded to existing instance. Exiting secondary JVM.");
            System.exit(0);
            return;
        }

        try {
            new ClientHandler(null);
        } catch (Throwable ignored) {}

        startIpcServer();
        setupContextMenuHook();

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
            openFileWithRetry(fileToOpen, 120);
        }
    }

    private static String cleanFilePath(String raw) {
        if (raw == null) return "";
        String s = raw.trim();
        while (s.startsWith("\uFEFF")) s = s.substring(1).trim();
        if (s.startsWith("\"") && s.endsWith("\"") && s.length() >= 2) s = s.substring(1, s.length() - 1).trim();
        while (s.startsWith("\uFEFF")) s = s.substring(1).trim();
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

    public static Object getDisplay() {
        try {
            Class<?> displayClass = Class.forName("org.eclipse.swt.widgets.Display");
            Field defaultField = displayClass.getDeclaredField("Default");
            defaultField.setAccessible(true);
            Object defaultDisplay = defaultField.get(null);
            if (defaultDisplay != null) return defaultDisplay;

            Field displaysField = displayClass.getDeclaredField("Displays");
            displaysField.setAccessible(true);
            Object[] displays = (Object[]) displaysField.get(null);
            if (displays != null) {
                for (Object d : displays) {
                    if (d != null) return d;
                }
            }
            return null;
        } catch (Throwable t) {
            return null;
        }
    }

    public static boolean isDisposed(Object swtObj) {
        if (swtObj == null) return true;
        try {
            Method m = swtObj.getClass().getMethod("isDisposed");
            return (Boolean) m.invoke(swtObj);
        } catch (Throwable t) {
            return true;
        }
    }

    public static void runOnDisplay(Object display, Runnable runnable) {
        if (display == null) return;
        try {
            Method m = display.getClass().getMethod("asyncExec", Runnable.class);
            m.invoke(display, runnable);
        } catch (Throwable t) {
            log("Error invoking asyncExec: " + t.getMessage());
        }
    }

    public static void runTimer(Object display, int delayMs, Runnable runnable) {
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
                            Object nfg = extractTargetObject(l, "com.automate.formgen.NewFormGen", 0);
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

    public static Object extractTargetObject(Object obj, String targetClassName, int depth) {
        if (obj == null || depth > 5) return null;
        Class<?> cls = obj.getClass();
        if (cls.getName().equals(targetClassName)) return obj;
        if (cls.getSuperclass() != null && cls.getSuperclass().getName().equals(targetClassName)) return obj;

        // 1. If obj is a Widget, inspect its eventTable.listeners
        try {
            Class<?> widgetClass = Class.forName("org.eclipse.swt.widgets.Widget");
            if (widgetClass.isInstance(obj)) {
                Field eventTableField = widgetClass.getDeclaredField("eventTable");
                eventTableField.setAccessible(true);
                Object eventTable = eventTableField.get(obj);
                if (eventTable != null) {
                    Field listenersField = eventTable.getClass().getDeclaredField("listeners");
                    listenersField.setAccessible(true);
                    Object[] listeners = (Object[]) listenersField.get(eventTable);
                    if (listeners != null) {
                        for (Object l : listeners) {
                            if (l != null) {
                                Object found = extractTargetObject(l, targetClassName, depth + 1);
                                if (found != null) return found;
                            }
                        }
                    }
                }
            }
        } catch (Throwable ignored) {}

        // 2. Inspect all declared fields of this class and its superclasses
        Class<?> c = cls;
        while (c != null && c != Object.class) {
            for (Field f : c.getDeclaredFields()) {
                try {
                    f.setAccessible(true);
                    Object val = f.get(obj);
                    if (val != null) {
                        if (val.getClass().getName().equals(targetClassName)) return val;
                        if (val.getClass().getSuperclass() != null && val.getClass().getSuperclass().getName().equals(targetClassName)) return val;

                        String fName = f.getName();
                        if (fName.startsWith("this$") || fName.startsWith("arg$") || fName.equals("eventListener")) {
                            Object found = extractTargetObject(val, targetClassName, depth + 1);
                            if (found != null) return found;
                        }
                    }
                } catch (Throwable ignored) {}
            }
            c = c.getSuperclass();
        }

        // 3. If obj is a Composite, check its children
        try {
            Class<?> compositeClass = Class.forName("org.eclipse.swt.widgets.Composite");
            if (compositeClass.isInstance(obj)) {
                Method getChildrenMethod = compositeClass.getMethod("getChildren");
                Object[] children = (Object[]) getChildrenMethod.invoke(obj);
                if (children != null) {
                    for (Object child : children) {
                        Object found = extractTargetObject(child, targetClassName, depth + 1);
                        if (found != null) return found;
                    }
                }
            }
        } catch (Throwable ignored) {}

        return null;
    }

    public static Object findChildButton(Object parent, String textSubstring) {
        if (parent == null || isDisposed(parent)) return null;
        try {
            Class<?> buttonClass = Class.forName("org.eclipse.swt.widgets.Button");
            if (buttonClass.isInstance(parent)) {
                Method getTextMethod = parent.getClass().getMethod("getText");
                String t = (String) getTextMethod.invoke(parent);
                if (t != null && (textSubstring == null || t.toLowerCase().contains(textSubstring.toLowerCase()))) {
                    return parent;
                }
            }
            Class<?> compositeClass = Class.forName("org.eclipse.swt.widgets.Composite");
            if (compositeClass.isInstance(parent)) {
                Method getChildrenMethod = compositeClass.getMethod("getChildren");
                Object[] children = (Object[]) getChildrenMethod.invoke(parent);
                if (children != null) {
                    for (Object c : children) {
                        Object found = findChildButton(c, textSubstring);
                        if (found != null) return found;
                    }
                }
            }
        } catch (Throwable ignored) {}
        return null;
    }

    public static Object findChildControl(Object parent, String classNameSubstring) {
        if (parent == null || isDisposed(parent)) return null;
        try {
            if (parent.getClass().getName().contains(classNameSubstring)) {
                return parent;
            }
            Class<?> compositeClass = Class.forName("org.eclipse.swt.widgets.Composite");
            if (compositeClass.isInstance(parent)) {
                Method getChildrenMethod = compositeClass.getMethod("getChildren");
                Object[] children = (Object[]) getChildrenMethod.invoke(parent);
                if (children != null) {
                    for (Object c : children) {
                        Object found = findChildControl(c, classNameSubstring);
                        if (found != null) return found;
                    }
                }
            }
        } catch (Throwable ignored) {}
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

                    lastOpenedFilePath = cleanPath;
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

    public static void pushForm(final String targetUuid, final String rawPath) {
        Object display = getDisplay();
        if (display == null || isDisposed(display)) {
            log("Display is null or disposed when pushing form: " + rawPath);
            return;
        }

        runOnDisplay(display, new Runnable() {
            @Override
            public void run() {
                try {
                    if (!discoverFormGen()) {
                        log("Could not discover FormGen instance for pushForm: " + rawPath);
                        openFile(rawPath);
                        return;
                    }

                    String cleanPath = cleanFilePath(rawPath);
                    File file = new File(cleanPath);
                    if (!file.exists()) {
                        log("File does not exist for pushForm: " + cleanPath);
                        return;
                    }

                    String resolvedUuid = (targetUuid != null && !targetUuid.trim().isEmpty())
                        ? targetUuid.trim()
                        : readUuidFromFormgenFile(cleanPath);

                    log("pushForm: Target UUID=" + resolvedUuid + ", File=" + cleanPath);

                    Object mainTabFolder = null;
                    try {
                        Field f = formGenInstance.getClass().getDeclaredField("mainTabFolder");
                        f.setAccessible(true);
                        mainTabFolder = f.get(formGenInstance);
                    } catch (Throwable t) {
                        log("Could not get mainTabFolder: " + t.getMessage());
                    }

                    Object matchingTab = null;
                    Object matchingViewer = null;

                    if (mainTabFolder != null && !isDisposed(mainTabFolder) && resolvedUuid != null && !resolvedUuid.isEmpty()) {
                        try {
                            Method getItemsMethod = mainTabFolder.getClass().getMethod("getItems");
                            Object[] tabItems = (Object[]) getItemsMethod.invoke(mainTabFolder);
                            if (tabItems != null) {
                                for (Object tabItem : tabItems) {
                                    if (tabItem == null || isDisposed(tabItem)) continue;
                                    try {
                                        Method getControlMethod = tabItem.getClass().getMethod("getControl");
                                        Object viewer = getControlMethod.invoke(tabItem);
                                        if (viewer != null && !isDisposed(viewer)) {
                                            String tabUuid = null;
                                            try {
                                                Method getFormDefMethod = viewer.getClass().getMethod("getFormDef");
                                                Object formDef = getFormDefMethod.invoke(viewer);
                                                tabUuid = getFormDefUuid(formDef);
                                            } catch (Throwable ignored) {}

                                            if (tabUuid == null) {
                                                try {
                                                    Method getFileMethod = viewer.getClass().getMethod("getCurrentFormgenFile");
                                                    File tabFile = (File) getFileMethod.invoke(viewer);
                                                    if (tabFile != null && tabFile.exists()) {
                                                        tabUuid = readUuidFromFormgenFile(tabFile.getAbsolutePath());
                                                    }
                                                } catch (Throwable ignored) {}
                                            }

                                            if (tabUuid != null && tabUuid.trim().equalsIgnoreCase(resolvedUuid)) {
                                                matchingTab = tabItem;
                                                matchingViewer = viewer;
                                                break;
                                            }
                                        }
                                    } catch (Throwable tabErr) {
                                        log("Error checking tab: " + tabErr.getMessage());
                                    }
                                }
                            }
                        } catch (Throwable t) {
                            log("Error iterating tab items: " + t.getMessage());
                        }
                    }

                    Class<?> formOpenModeClass = Class.forName("com.automate.formgen.FormOpenMode");
                    Field normalField = formOpenModeClass.getField("Normal");
                    Object normalMode = normalField.get(null);

                    if (matchingTab != null && matchingViewer != null) {
                        log("Found matching tab for UUID " + resolvedUuid + ". Focusing tab and reloading form...");

                        // Focus tab
                        try {
                            Method setSelectionMethod = mainTabFolder.getClass().getMethod("setSelection", Class.forName("org.eclipse.swt.custom.CTabItem"));
                            setSelectionMethod.invoke(mainTabFolder, matchingTab);
                        } catch (Throwable t) {
                            log("Error setting tab selection: " + t.getMessage());
                        }

                        try {
                            Field fvField = formGenInstance.getClass().getDeclaredField("formViewer");
                            fvField.setAccessible(true);
                            fvField.set(formGenInstance, matchingViewer);
                        } catch (Throwable ignored) {}

                        try {
                            Method onTabSel = formGenInstance.getClass().getDeclaredMethod("onTabSelected", Class.forName("org.eclipse.swt.custom.CTabItem"));
                            onTabSel.setAccessible(true);
                            onTabSel.invoke(formGenInstance, matchingTab);
                        } catch (Throwable ignored) {}

                        // Reload form in viewer
                        try {
                            Method openFormFileMethod = matchingViewer.getClass().getMethod("openFormFile", formOpenModeClass, File.class, Class.forName("com.automate.formgen.dialogs.checkinout.CheckoutInfo"));
                            openFormFileMethod.setAccessible(true);
                            Object res = openFormFileMethod.invoke(matchingViewer, normalMode, file, null);
                            log("Form reloaded in existing tab: " + res);
                        } catch (Throwable t) {
                            log("Error reloading form in viewer: " + t.getMessage());
                        }

                        // Refresh titles and return focus
                        try {
                            Method m = formGenInstance.getClass().getMethod("setCurrentTabTitle");
                            m.invoke(formGenInstance);
                        } catch (Throwable ignored) {}
                        try {
                            Method m = formGenInstance.getClass().getDeclaredMethod("setCurrentFormgenFilenameTitle");
                            m.setAccessible(true);
                            m.invoke(formGenInstance);
                        } catch (Throwable ignored) {}
                        try {
                            Method m = formGenInstance.getClass().getMethod("returnFocusToForm");
                            m.invoke(formGenInstance);
                        } catch (Throwable ignored) {}

                        activateMainShell();
                    } else {
                        log("No matching tab for UUID " + resolvedUuid + ". Opening in new tab...");
                        try {
                            Method createNewViewerMethod = formGenInstance.getClass().getDeclaredMethod("createNewPdfViewer");
                            createNewViewerMethod.setAccessible(true);
                            Object newViewer = createNewViewerMethod.invoke(formGenInstance);

                            Method openFormFileMethod = newViewer.getClass().getMethod("openFormFile", formOpenModeClass, File.class, Class.forName("com.automate.formgen.dialogs.checkinout.CheckoutInfo"));
                            openFormFileMethod.setAccessible(true);
                            Object res = openFormFileMethod.invoke(newViewer, normalMode, file, null);
                            log("New form loaded in new viewer: " + res);

                            Method createNewTabMethod = formGenInstance.getClass().getDeclaredMethod("createNewTab", Class.forName("com.automate.formgen.PdfViewer"));
                            createNewTabMethod.setAccessible(true);
                            createNewTabMethod.invoke(formGenInstance, newViewer);

                            try {
                                Method m = formGenInstance.getClass().getMethod("setCurrentTabTitle");
                                m.invoke(formGenInstance);
                            } catch (Throwable ignored) {}
                            try {
                                Method m = formGenInstance.getClass().getDeclaredMethod("setCurrentFormgenFilenameTitle");
                                m.setAccessible(true);
                                m.invoke(formGenInstance);
                            } catch (Throwable ignored) {}
                            try {
                                Method m = formGenInstance.getClass().getMethod("returnFocusToForm");
                                m.invoke(formGenInstance);
                            } catch (Throwable ignored) {}
                        } catch (Throwable t) {
                            log("Direct createNewTab failed, falling back to openFormgenFile: " + t.getMessage());
                            Method openMethod = formGenInstance.getClass().getDeclaredMethod("openFormgenFile", File.class, boolean.class);
                            openMethod.setAccessible(true);
                            openMethod.invoke(formGenInstance, file, false);
                        }

                        activateMainShell();
                    }
                } catch (Throwable t) {
                    StringWriter sw = new StringWriter();
                    t.printStackTrace(new PrintWriter(sw));
                    log("Error in pushForm: " + sw.toString());
                }
            }
        });
    }

    public static String getFormDefUuid(Object formDef) {
        if (formDef == null) return null;
        try {
            Method m = formDef.getClass().getMethod("getPublishedUUID");
            Object u = m.invoke(formDef);
            if (u != null) return u.toString();
        } catch (Throwable ignored) {}
        try {
            Method m = formDef.getClass().getMethod("getUUID");
            Object u = m.invoke(formDef);
            if (u != null) return u.toString();
        } catch (Throwable ignored) {}
        for (Method m : formDef.getClass().getMethods()) {
            if (m.getName().toLowerCase().contains("uuid") && m.getParameterCount() == 0) {
                try {
                    Object u = m.invoke(formDef);
                    if (u != null) return u.toString();
                } catch (Throwable ignored) {}
            }
        }
        for (Field f : formDef.getClass().getDeclaredFields()) {
            if (f.getName().equalsIgnoreCase("publishedUUID") || f.getName().equalsIgnoreCase("uuid")) {
                try {
                    f.setAccessible(true);
                    Object val = f.get(formDef);
                    if (val != null) return val.toString();
                } catch (Throwable ignored) {}
            }
        }
        return null;
    }

    private static String readUuidFromFormgenFile(String path) {
        if (path == null) return null;
        try {
            File f = new File(path);
            if (!f.exists()) return null;
            BufferedReader reader = new BufferedReader(new InputStreamReader(new FileInputStream(f), "UTF-8"));
            String line;
            Pattern p = Pattern.compile("publishedUUID\\s*=\\s*\"([^\"]+)\"", Pattern.CASE_INSENSITIVE);
            while ((line = reader.readLine()) != null) {
                Matcher m = p.matcher(line);
                if (m.find()) {
                    reader.close();
                    return m.group(1);
                }
            }
            reader.close();
        } catch (Throwable ignored) {}
        return null;
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
                        try {
                            final Socket socket = serverSocket.accept();
                            new Thread(new ClientHandler(socket), "FormGenAgent-Client").start();
                        } catch (Throwable clientErr) {
                            log("IPC Server accept error: " + clientErr.getMessage());
                        }
                    }
                } catch (Throwable t) {
                    log("IPC Server fatal error: " + t.getMessage());
                }
            }
        }, "FormGenAgent-IPC");
        serverThread.setDaemon(true);
        serverThread.start();
    }

    private static class ClientHandler implements Runnable {
        private final Socket socket;
        ClientHandler(Socket socket) {
            this.socket = socket;
        }

        @Override
        public void run() {
            if (socket == null) return;
            try {
                handleClient(socket);
            } catch (Throwable ignored) {
            } finally {
                try { socket.close(); } catch (Throwable ignored) {}
            }
        }
    }

    private static void handleClient(Socket socket) {
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
                } else if (line.startsWith("SCMT_PATH:")) {
                    scmtPath = line.substring(10).trim();
                    log("Updated SCMT executable path: " + scmtPath);
                    OutputStream out = socket.getOutputStream();
                    out.write("OK\n".getBytes("UTF-8"));
                    out.flush();
                } else if (line.startsWith("VARS:")) {
                    String varsJson = line.substring(5).trim();
                    updateLiveVariablesFromJson(varsJson);
                    OutputStream out = socket.getOutputStream();
                    out.write("OK\n".getBytes("UTF-8"));
                    out.flush();
                } else if (line.equalsIgnoreCase("GET_ACTIVE_FORM")) {
                    String formPath = getActiveFormFilePath();
                    OutputStream out = socket.getOutputStream();
                    out.write(((formPath != null ? formPath : "") + "\n").getBytes("UTF-8"));
                    out.flush();
                } else if (line.startsWith("PUSH_FORM:")) {
                    String payload = line.substring(10).trim();
                    String uuid = null;
                    String filePath = payload;
                    int pipe = payload.indexOf('|');
                    if (pipe != -1) {
                        uuid = payload.substring(0, pipe).trim();
                        filePath = payload.substring(pipe + 1).trim();
                    }
                    pushForm(uuid, filePath);
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
        } catch (Throwable t) {
            log("Error handling IPC client: " + t.getMessage());
        }
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

    // ==========================================
    // Context Menu & UI Hooks via Reflection
    // ==========================================

    private static synchronized void setupContextMenuHook() {
        if (menuHookInstalled) return;
        menuHookInstalled = true;

        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    Object display = null;
                    for (int i = 0; i < 60; i++) {
                        display = getDisplay();
                        if (display != null && !isDisposed(display)) break;
                        Thread.sleep(500);
                    }
                    if (display == null) {
                        log("Display not available for context menu hook.");
                        return;
                    }

                    final Object disp = display;
                    runOnDisplay(disp, new Runnable() {
                        @Override
                        public void run() {
                            installMenuFilters(disp);
                        }
                    });
                } catch (Throwable t) {
                    log("Error setting up context menu hook: " + t.getMessage());
                }
            }
        }, "FormGenAgent-MenuHook").start();
    }

    // ==========================================
    // StyledText / RainbowParenText Undo Manager
    // ==========================================

    private static class EditRecord {
        final int start;
        final String insertedText;
        final String replacedText;

        EditRecord(int start, String insertedText, String replacedText) {
            this.start = start;
            this.insertedText = insertedText != null ? insertedText : "";
            this.replacedText = replacedText != null ? replacedText : "";
        }
    }

    private static class StyledTextUndoManager {
        private final Object control;
        private final LinkedList<EditRecord> undoStack = new LinkedList<EditRecord>();
        private final LinkedList<EditRecord> redoStack = new LinkedList<EditRecord>();
        private boolean isUndoing = false;
        private static final int MAX_STACK_SIZE = 500;

        StyledTextUndoManager(Object control) {
            this.control = control;
        }

        public synchronized void recordChange(int start, String insertedText, String replacedText) {
            if (isUndoing) return;
            if (insertedText.isEmpty() && replacedText.isEmpty()) return;

            undoStack.addLast(new EditRecord(start, insertedText, replacedText));
            if (undoStack.size() > MAX_STACK_SIZE) {
                undoStack.removeFirst();
            }
            redoStack.clear();
        }

        public synchronized boolean canUndo() {
            return !undoStack.isEmpty();
        }

        public synchronized boolean canRedo() {
            return !redoStack.isEmpty();
        }

        public synchronized void undo() {
            if (undoStack.isEmpty() || control == null || isDisposed(control)) return;
            final EditRecord rec = undoStack.removeLast();
            isUndoing = true;
            try {
                Method replaceMethod = control.getClass().getMethod("replaceTextRange", int.class, int.class, String.class);
                replaceMethod.invoke(control, rec.start, rec.insertedText.length(), rec.replacedText);

                int newOffset = rec.start + rec.replacedText.length();
                Method setSelectionMethod = control.getClass().getMethod("setSelection", int.class, int.class);
                setSelectionMethod.invoke(control, newOffset, newOffset);

                redoStack.addLast(rec);
                if (redoStack.size() > MAX_STACK_SIZE) {
                    redoStack.removeFirst();
                }
            } catch (Throwable t) {
                log("Error during undo: " + t.getMessage());
            } finally {
                isUndoing = false;
            }
        }

        public synchronized void redo() {
            if (redoStack.isEmpty() || control == null || isDisposed(control)) return;
            final EditRecord rec = redoStack.removeLast();
            isUndoing = true;
            try {
                Method replaceMethod = control.getClass().getMethod("replaceTextRange", int.class, int.class, String.class);
                replaceMethod.invoke(control, rec.start, rec.replacedText.length(), rec.insertedText);

                int newOffset = rec.start + rec.insertedText.length();
                Method setSelectionMethod = control.getClass().getMethod("setSelection", int.class, int.class);
                setSelectionMethod.invoke(control, newOffset, newOffset);

                undoStack.addLast(rec);
                if (undoStack.size() > MAX_STACK_SIZE) {
                    undoStack.removeFirst();
                }
            } catch (Throwable t) {
                log("Error during redo: " + t.getMessage());
            } finally {
                isUndoing = false;
            }
        }
    }

    private static synchronized StyledTextUndoManager getOrCreateUndoManager(final Object control) {
        if (control == null || isDisposed(control)) return null;
        try {
            Method getDataMethod = control.getClass().getMethod("getData", String.class);
            Object existing = getDataMethod.invoke(control, "SoleraUndoManager");
            if (existing instanceof StyledTextUndoManager) {
                return (StyledTextUndoManager) existing;
            }

            final StyledTextUndoManager mgr = new StyledTextUndoManager(control);
            Method setDataMethod = control.getClass().getMethod("setData", String.class, Object.class);
            setDataMethod.invoke(control, "SoleraUndoManager", mgr);

            Class<?> extModListenerClass = Class.forName("org.eclipse.swt.custom.ExtendedModifyListener");
            InvocationHandler handler = new InvocationHandler() {
                @Override
                public Object invoke(Object proxy, Method method, Object[] args) throws Throwable {
                    if ("modifyText".equals(method.getName()) && args != null && args.length == 1) {
                        try {
                            Object eventObj = args[0];
                            Field startField = eventObj.getClass().getField("start");
                            Field lengthField = eventObj.getClass().getField("length");
                            Field replacedTextField = eventObj.getClass().getField("replacedText");

                            int start = startField.getInt(eventObj);
                            int length = lengthField.getInt(eventObj);
                            String replacedText = (String) replacedTextField.get(eventObj);
                            if (replacedText == null) replacedText = "";

                            String insertedText = "";
                            if (length > 0) {
                                try {
                                    Method getTextRangeMethod = control.getClass().getMethod("getTextRange", int.class, int.class);
                                    insertedText = (String) getTextRangeMethod.invoke(control, start, length);
                                } catch (Throwable ignored) {}
                            }
                            mgr.recordChange(start, insertedText, replacedText);
                        } catch (Throwable t) {
                            log("Error in modifyText handler: " + t.getMessage());
                        }
                    }
                    return null;
                }
            };

            Object listenerProxy = Proxy.newProxyInstance(
                control.getClass().getClassLoader(),
                new Class<?>[] { extModListenerClass },
                handler
            );

            Method addListenerMethod = control.getClass().getMethod("addExtendedModifyListener", extModListenerClass);
            addListenerMethod.invoke(control, listenerProxy);

            log("Installed StyledTextUndoManager on: " + control.getClass().getName());
            return mgr;
        } catch (Throwable t) {
            log("Error installing StyledTextUndoManager: " + t.getMessage());
            return null;
        }
    }

    private static void attachUndoManagersRecursively(Object parent) {
        if (parent == null || isDisposed(parent)) return;
        try {
            String clsName = parent.getClass().getName();
            if (clsName.contains("RainbowParenText") || clsName.contains("StyledText")) {
                getOrCreateUndoManager(parent);
            }
            Class<?> compositeClass = Class.forName("org.eclipse.swt.widgets.Composite");
            if (compositeClass.isInstance(parent)) {
                Method getChildrenMethod = compositeClass.getMethod("getChildren");
                Object[] children = (Object[]) getChildrenMethod.invoke(parent);
                if (children != null) {
                    for (Object c : children) {
                        attachUndoManagersRecursively(c);
                    }
                }
            }
        } catch (Throwable ignored) {}
    }

    private static void installMenuFilters(final Object display) {
        try {
            Class<?> listenerClass = Class.forName("org.eclipse.swt.widgets.Listener");

            InvocationHandler handler = new InvocationHandler() {
                @Override
                public Object invoke(Object proxy, Method method, Object[] args) throws Throwable {
                    if ("handleEvent".equals(method.getName()) && args != null && args.length == 1) {
                        handleSwtEvent(display, args[0]);
                    }
                    return null;
                }
            };

            Object listenerProxy = Proxy.newProxyInstance(
                display.getClass().getClassLoader(),
                new Class<?>[] { listenerClass },
                handler
            );

            Method addFilterMethod = display.getClass().getMethod("addFilter", int.class, listenerClass);
            addFilterMethod.invoke(display, SWT_KeyDown, listenerProxy);     // SWT.KeyDown = 1
            addFilterMethod.invoke(display, SWT_FocusIn, listenerProxy);     // SWT.FocusIn = 15
            addFilterMethod.invoke(display, SWT_Show, listenerProxy);        // SWT.Show = 22
            addFilterMethod.invoke(display, SWT_Activate, listenerProxy);    // SWT.Activate = 26
            addFilterMethod.invoke(display, SWT_MenuDetect, listenerProxy);  // SWT.MenuDetect = 35

            log("SWT context menu & dialog filters installed (KeyDown=1, FocusIn=15, Show=22, Activate=26, MenuDetect=35).");
            startShellEnhanceWatcher(display);
        } catch (Throwable t) {
            log("Error installing SWT menu filters: " + t.getMessage());
        }
    }

    private static void startShellEnhanceWatcher(final Object display) {
        runTimer(display, 500, new Runnable() {
            @Override
            public void run() {
                try {
                    if (display == null || isDisposed(display)) return;
                    Method getShellsMethod = display.getClass().getMethod("getShells");
                    Object[] shells = (Object[]) getShellsMethod.invoke(display);
                    if (shells != null) {
                        for (Object s : shells) {
                            if (s != null && !isDisposed(s)) {
                                enhanceDialogShell(display, s);
                                attachUndoManagersRecursively(s);
                            }
                        }
                    }
                } catch (Throwable ignored) {}
                if (!isDisposed(display)) {
                    runTimer(display, 500, this);
                }
            }
        });
    }

    private static void handleSwtEvent(Object display, Object eventObj) {
        if (eventObj == null) return;
        try {
            Field typeField = eventObj.getClass().getField("type");
            int eventType = typeField.getInt(eventObj);

            Field widgetField = eventObj.getClass().getField("widget");
            Object widget = widgetField.get(eventObj);
            if (widget == null || isDisposed(widget)) return;

            Class<?> menuClass = Class.forName("org.eclipse.swt.widgets.Menu");
            Class<?> controlClass = Class.forName("org.eclipse.swt.widgets.Control");
            Class<?> shellClass = Class.forName("org.eclipse.swt.widgets.Shell");

            if (eventType == SWT_KeyDown) {
                if (widget != null) {
                    String clsName = widget.getClass().getName();
                    if (clsName.contains("RainbowParenText") || clsName.contains("StyledText")) {
                        StyledTextUndoManager mgr = getOrCreateUndoManager(widget);
                        Field stateMaskField = eventObj.getClass().getField("stateMask");
                        Field keyCodeField = eventObj.getClass().getField("keyCode");
                        Field characterField = eventObj.getClass().getField("character");
                        int stateMask = stateMaskField.getInt(eventObj);
                        int keyCode = keyCodeField.getInt(eventObj);
                        char character = characterField.getChar(eventObj);

                        boolean isCtrl = (stateMask & SWT_MOD1) != 0;
                        boolean isShift = (stateMask & SWT_SHIFT) != 0;

                        if (isCtrl && (keyCode == 'z' || keyCode == 'Z' || character == 26)) {
                            if (mgr != null) {
                                if (isShift) {
                                    mgr.redo();
                                } else {
                                    mgr.undo();
                                }
                                Field doitField = eventObj.getClass().getField("doit");
                                doitField.setBoolean(eventObj, false);
                                return;
                            }
                        } else if (isCtrl && (keyCode == 'y' || keyCode == 'Y' || character == 25)) {
                            if (mgr != null) {
                                mgr.redo();
                                Field doitField = eventObj.getClass().getField("doit");
                                doitField.setBoolean(eventObj, false);
                                return;
                            }
                        }
                    }
                }
            } else if (eventType == SWT_FocusIn) {
                if (widget != null) {
                    String clsName = widget.getClass().getName();
                    if (clsName.contains("RainbowParenText") || clsName.contains("StyledText")) {
                        getOrCreateUndoManager(widget);
                    }
                }
            } else if (eventType == SWT_Show) {
                if (menuClass.isInstance(widget)) {
                    Method getStyleMethod = widget.getClass().getMethod("getStyle");
                    int style = (Integer) getStyleMethod.invoke(widget);
                    if ((style & SWT_POP_UP) != 0) {
                        try {
                            Method getDataMethod = widget.getClass().getMethod("getData", String.class);
                            if (Boolean.TRUE.equals(getDataMethod.invoke(widget, "SoleraInjected"))) {
                                return;
                            }
                        } catch (Throwable ignored) {}
                        injectSoleraMenuItems(display, widget);
                    }
                } else if (shellClass.isInstance(widget)) {
                    enhanceDialogShell(display, widget);
                    attachUndoManagersRecursively(widget);
                }
            } else if (eventType == SWT_Activate) {
                if (shellClass.isInstance(widget)) {
                    enhanceDialogShell(display, widget);
                    attachUndoManagersRecursively(widget);
                }
            } else if (eventType == SWT_MenuDetect) {
                if (controlClass.isInstance(widget)) {
                    String clsName = widget.getClass().getName();
                    // Intercept black code window (RainbowParenText), StyledText
                    if (clsName.contains("RainbowParenText") || clsName.contains("StyledText")) {
                        attachCodeEditorContextMenu(display, widget, eventObj);
                        return;
                    }

                    // Only intercept text input controls (never PdfViewer, Canvas, Shell, or general containers)
                    if ((clsName.contains("Text") || clsName.contains("Combo")) &&
                        !clsName.contains("Canvas") && !clsName.contains("PdfViewer") && !clsName.contains("Shell")) {
                        Method getMenuMethod = widget.getClass().getMethod("getMenu");
                        Object existingMenu = getMenuMethod.invoke(widget);
                        if (existingMenu == null) {
                            attachStandardTextContextMenu(display, widget, eventObj);
                        }
                    }
                }
            }
        } catch (Throwable t) {
            log("Error in handleSwtEvent: " + t.getMessage());
        }
    }

    // ==========================================
    // Code Editor RMB Menu (Black Window / Prompts)
    // ==========================================

    private static void attachCodeEditorContextMenu(final Object display, final Object control, Object eventObj) {
        try {
            log("Attaching Code Editor Context Menu to: " + control.getClass().getName());
            Class<?> menuClass = Class.forName("org.eclipse.swt.widgets.Menu");
            Class<?> controlClass = Class.forName("org.eclipse.swt.widgets.Control");
            Class<?> menuItemClass = Class.forName("org.eclipse.swt.widgets.MenuItem");
            Class<?> listenerClass = Class.forName("org.eclipse.swt.widgets.Listener");

            Constructor<?> menuCons = menuClass.getConstructor(controlClass);
            Object menu = menuCons.newInstance(control);

            Constructor<?> itemCons = menuItemClass.getConstructor(menuClass, int.class);
            Constructor<?> subMenuCons = menuClass.getConstructor(menuItemClass);
            Method addListenerMethod = menuItemClass.getMethod("addListener", int.class, listenerClass);

            // 0. Undo, Redo
            final StyledTextUndoManager undoMgr = getOrCreateUndoManager(control);
            Object itemUndo = itemCons.newInstance(menu, SWT_PUSH);
            itemUndo.getClass().getMethod("setText", String.class).invoke(itemUndo, "Undo\tCtrl+Z");
            if (undoMgr != null && !undoMgr.canUndo()) {
                itemUndo.getClass().getMethod("setEnabled", boolean.class).invoke(itemUndo, false);
            }
            addListenerMethod.invoke(itemUndo, SWT_Selection, createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                @Override
                public void run() {
                    if (undoMgr != null) undoMgr.undo();
                }
            }));

            Object itemRedo = itemCons.newInstance(menu, SWT_PUSH);
            itemRedo.getClass().getMethod("setText", String.class).invoke(itemRedo, "Redo\tCtrl+Y");
            if (undoMgr != null && !undoMgr.canRedo()) {
                itemRedo.getClass().getMethod("setEnabled", boolean.class).invoke(itemRedo, false);
            }
            addListenerMethod.invoke(itemRedo, SWT_Selection, createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                @Override
                public void run() {
                    if (undoMgr != null) undoMgr.redo();
                }
            }));

            itemCons.newInstance(menu, SWT_SEPARATOR);

            // 1. Cut, Copy, Paste, Select All
            addTextActionItem(display, menu, control, "Cut\tCtrl+X", "cut");
            addTextActionItem(display, menu, control, "Copy\tCtrl+C", "copy");
            addTextActionItem(display, menu, control, "Paste\tCtrl+V", "paste");
            addTextActionItem(display, menu, control, "Select All\tCtrl+A", "selectAll");

            // Separator
            itemCons.newInstance(menu, SWT_SEPARATOR);

            // 2. Parse in Code Snippets
            Object itemParse = itemCons.newInstance(menu, SWT_PUSH);
            itemParse.getClass().getMethod("setText", String.class).invoke(itemParse, "🔬 Parse in Code Snippets");
            Object listenerParse = createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                @Override
                public void run() {
                    String selected = getSelectedTextFromFocusControl(display);
                    if (selected.trim().isEmpty()) {
                        try {
                            Method m = control.getClass().getMethod("getText");
                            selected = (String) m.invoke(control);
                        } catch (Throwable ignored) {}
                    }
                    notifyScmtParseCode(selected);
                }
            });
            addListenerMethod.invoke(itemParse, SWT_Selection, listenerParse);

            // 3. Paste from Code Snippets / SCMT Active Formula
            Object itemPasteFormula = itemCons.newInstance(menu, SWT_PUSH);
            itemPasteFormula.getClass().getMethod("setText", String.class).invoke(itemPasteFormula, "📋 Paste from Code Snippets / Formula");
            Object listenerPasteFormula = createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                @Override
                public void run() {
                    runOnDisplay(display, new Runnable() {
                        @Override
                        public void run() {
                            String codeToPaste = liveVariables.get("ActiveTemplateText");
                            if (codeToPaste == null || codeToPaste.trim().isEmpty()) {
                                codeToPaste = getClipboardText();
                            }
                            insertTextIntoFocusControl(display, codeToPaste);
                        }
                    });
                }
            });
            addListenerMethod.invoke(itemPasteFormula, SWT_Selection, listenerPasteFormula);

            // 4. Inject Solera Tools & variables cleanly without duplicating
            injectSoleraMenuItems(display, menu);

            Method setDataMethod = menu.getClass().getMethod("setData", String.class, Object.class);
            setDataMethod.invoke(menu, "SoleraInjected", Boolean.TRUE);

            Method setMenuMethod = control.getClass().getMethod("setMenu", menuClass);
            setMenuMethod.invoke(control, menu);

            Method setVisibleMethod = menu.getClass().getMethod("setVisible", boolean.class);
            setVisibleMethod.invoke(menu, true);

            Field doitField = eventObj.getClass().getField("doit");
            doitField.setBoolean(eventObj, false);
            log("Successfully opened Code Editor Context Menu.");
        } catch (Throwable t) {
            log("Error in attachCodeEditorContextMenu: " + t.getMessage());
        }
    }

    private static void attachStandardTextContextMenu(Object display, Object control, Object eventObj) {
        try {
            Class<?> menuClass = Class.forName("org.eclipse.swt.widgets.Menu");
            Class<?> controlClass = Class.forName("org.eclipse.swt.widgets.Control");
            Constructor<?> menuCons = menuClass.getConstructor(controlClass);
            Object newMenu = menuCons.newInstance(control);

            String clsName = control.getClass().getName();
            if (clsName.contains("Text")) {
                addTextActionItem(display, newMenu, control, "Cut", "cut");
                addTextActionItem(display, newMenu, control, "Copy", "copy");
                addTextActionItem(display, newMenu, control, "Paste", "paste");
                addTextActionItem(display, newMenu, control, "Select All", "selectAll");
            }

            injectSoleraMenuItems(display, newMenu);

            Method setDataMethod = newMenu.getClass().getMethod("setData", String.class, Object.class);
            setDataMethod.invoke(newMenu, "SoleraInjected", Boolean.TRUE);

            Method setMenuMethod = control.getClass().getMethod("setMenu", menuClass);
            setMenuMethod.invoke(control, newMenu);

            Method setVisibleMethod = newMenu.getClass().getMethod("setVisible", boolean.class);
            setVisibleMethod.invoke(newMenu, true);

            Field doitField = eventObj.getClass().getField("doit");
            doitField.setBoolean(eventObj, false);
        } catch (Throwable t) {
            log("Error in attachStandardTextContextMenu: " + t.getMessage());
        }
    }

    private static void addTextActionItem(final Object display, Object menu, final Object control, String text, final String methodName) {
        try {
            Class<?> menuItemClass = Class.forName("org.eclipse.swt.widgets.MenuItem");
            Class<?> menuClass = Class.forName("org.eclipse.swt.widgets.Menu");
            Constructor<?> itemCons = menuItemClass.getConstructor(menuClass, int.class);
            Object item = itemCons.newInstance(menu, SWT_PUSH);
            item.getClass().getMethod("setText", String.class).invoke(item, text);

            Object listenerProxy = createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                @Override
                public void run() {
                    try {
                        Method m = control.getClass().getMethod(methodName);
                        m.invoke(control);
                    } catch (Throwable ignored) {}
                }
            });

            Method addListenerMethod = item.getClass().getMethod("addListener", int.class, Class.forName("org.eclipse.swt.widgets.Listener"));
            addListenerMethod.invoke(item, SWT_Selection, listenerProxy);
        } catch (Throwable ignored) {}
    }

    private static void injectSoleraMenuItems(final Object display, Object menu) {
        try {
            // Remove previous Solera items so we have a clean refresh
            Method getItemsMethod = menu.getClass().getMethod("getItems");
            Object[] items = (Object[]) getItemsMethod.invoke(menu);
            if (items != null) {
                for (int i = items.length - 1; i >= 0; i--) {
                    Object it = items[i];
                    String itText = "";
                    try { itText = (String) it.getClass().getMethod("getText").invoke(it); } catch (Throwable ignored) {}
                    Object tag = null;
                    try { tag = it.getClass().getMethod("getData", String.class).invoke(it, "SoleraItem"); } catch (Throwable ignored) {}
                    if (Boolean.TRUE.equals(tag) || itText.contains("Solera") || itText.contains("Parse Field") || itText.contains("FormGen Utilities")) {
                        it.getClass().getMethod("dispose").invoke(it);
                    }
                }
            }

            // Trigger non-blocking async variable refresh
            refreshLiveVariablesAsync();

            Class<?> menuItemClass = Class.forName("org.eclipse.swt.widgets.MenuItem");
            Class<?> menuClass = Class.forName("org.eclipse.swt.widgets.Menu");
            Class<?> listenerClass = Class.forName("org.eclipse.swt.widgets.Listener");
            Constructor<?> itemCons = menuItemClass.getConstructor(menuClass, int.class);
            Constructor<?> subMenuCons = menuClass.getConstructor(menuItemClass);
            Method addListenerMethod = menuItemClass.getMethod("addListener", int.class, listenerClass);
            Method setDataMethod = menuItemClass.getMethod("setData", String.class, Object.class);

            // Check if there is an active canvas field selected in PdfViewer
            final String fieldFormula = getSelectedFieldFormula();
            if (fieldFormula != null && !fieldFormula.trim().isEmpty()) {
                Object itemFieldFormula = itemCons.newInstance(menu, SWT_PUSH);
                itemFieldFormula.getClass().getMethod("setText", String.class).invoke(itemFieldFormula, "🔬 Parse Field Formula in Code Snippets");
                setDataMethod.invoke(itemFieldFormula, "SoleraItem", Boolean.TRUE);

                Object listenerField = createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                    @Override
                    public void run() {
                        notifyScmtParseCode(fieldFormula);
                    }
                });
                addListenerMethod.invoke(itemFieldFormula, SWT_Selection, listenerField);
            }

            // Separator
            Object sep = itemCons.newInstance(menu, SWT_SEPARATOR);
            setDataMethod.invoke(sep, "SoleraItem", Boolean.TRUE);

            // Cascade Item: ⚡ Solera Tools
            Object soleraCascade = itemCons.newInstance(menu, SWT_CASCADE);
            soleraCascade.getClass().getMethod("setText", String.class).invoke(soleraCascade, "⚡ Solera Tools");
            setDataMethod.invoke(soleraCascade, "SoleraItem", Boolean.TRUE);

            Object soleraSubMenu = subMenuCons.newInstance(soleraCascade);
            soleraCascade.getClass().getMethod("setMenu", menuClass).invoke(soleraCascade, soleraSubMenu);

            // Sub-item 1: 🛠 Open in FormGen Utilities
            Object itemOpenUtils = itemCons.newInstance(soleraSubMenu, SWT_PUSH);
            itemOpenUtils.getClass().getMethod("setText", String.class).invoke(itemOpenUtils, "🛠 Open in FormGen Utilities");
            Object listenerOpenUtils = createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                @Override
                public void run() {
                    notifyScmtOpenFormgenUtils(getActiveFormFilePath());
                }
            });
            addListenerMethod.invoke(itemOpenUtils, SWT_Selection, listenerOpenUtils);

            // Sub-item 2: 🔬 Parse Selection in Code Snippets
            Object itemParseCode = itemCons.newInstance(soleraSubMenu, SWT_PUSH);
            itemParseCode.getClass().getMethod("setText", String.class).invoke(itemParseCode, "🔬 Parse Selection in Code Snippets");
            Object listenerParseCode = createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                @Override
                public void run() {
                    String selectedText = getSelectedTextFromFocusControl(display);
                    notifyScmtParseCode(selectedText);
                }
            });
            addListenerMethod.invoke(itemParseCode, SWT_Selection, listenerParseCode);

            // Separator inside Solera Tools
            itemCons.newInstance(soleraSubMenu, SWT_SEPARATOR);

            // Sub-item 3: 📋 Quick Paste Variable ▶
            Object pasteCascade = itemCons.newInstance(soleraSubMenu, SWT_CASCADE);
            pasteCascade.getClass().getMethod("setText", String.class).invoke(pasteCascade, "📋 Quick Paste Variable");
            Object pasteSubMenu = subMenuCons.newInstance(pasteCascade);
            pasteCascade.getClass().getMethod("setMenu", menuClass).invoke(pasteCascade, pasteSubMenu);

            populateQuickPasteItems(display, pasteSubMenu, itemCons, addListenerMethod);

            // Mark the menu as having Solera injected
            Method setMenuData = menu.getClass().getMethod("setData", String.class, Object.class);
            setMenuData.invoke(menu, "SoleraInjected", Boolean.TRUE);
        } catch (Throwable t) {
            log("Error injecting Solera menu items: " + t.getMessage());
        }
    }

    private static String getSelectedFieldFormula() {
        if (formGenInstance == null) return null;
        try {
            Field fvField = formGenInstance.getClass().getDeclaredField("formViewer");
            fvField.setAccessible(true);
            Object formViewer = fvField.get(formGenInstance);
            if (formViewer == null) return null;

            Field sfField = formViewer.getClass().getDeclaredField("selectedFields");
            sfField.setAccessible(true);
            Object selectedFields = sfField.get(formViewer);
            if (selectedFields == null) return null;

            Method getSingleMethod = selectedFields.getClass().getMethod("getSingleSelectedField");
            Object field = getSingleMethod.invoke(selectedFields);
            if (field != null) {
                Method getExprMethod = field.getClass().getMethod("getExpression");
                return (String) getExprMethod.invoke(field);
            }
        } catch (Throwable ignored) {}
        return null;
    }

    private static void populateQuickPasteItems(final Object display, Object pasteSubMenu, Constructor<?> itemCons, Method addListenerMethod) {
        try {
            boolean hasItems = false;
            synchronized (liveVariables) {
                hasItems |= addVariableMenuItem(display, pasteSubMenu, itemCons, addListenerMethod, "Server ID", liveVariables.get("ServerId"));
                hasItems |= addVariableMenuItem(display, pasteSubMenu, itemCons, addListenerMethod, "Dealership", liveVariables.get("DealershipName"));
                hasItems |= addVariableMenuItem(display, pasteSubMenu, itemCons, addListenerMethod, "Case #", liveVariables.get("CaseNumber"));

                String companyStr = liveVariables.get("NotableCompanyNumbers");
                if (companyStr == null || companyStr.trim().isEmpty()) {
                    companyStr = liveVariables.get("PrimaryCompanyNumber");
                }
                hasItems |= addVariableMenuItem(display, pasteSubMenu, itemCons, addListenerMethod, "Company #", companyStr);

                hasItems |= addVariableMenuItem(display, pasteSubMenu, itemCons, addListenerMethod, "Contact Name", liveVariables.get("ContactName"));
                hasItems |= addVariableMenuItem(display, pasteSubMenu, itemCons, addListenerMethod, "Contact Phone", liveVariables.get("ContactPhone"));
                hasItems |= addVariableMenuItem(display, pasteSubMenu, itemCons, addListenerMethod, "Contact Email", liveVariables.get("ContactEmail"));

                String tmplText = liveVariables.get("ActiveTemplateText");
                if (tmplText != null && !tmplText.trim().isEmpty()) {
                    String tmplName = liveVariables.get("ActiveTemplateName");
                    String label = (tmplName != null && !tmplName.trim().isEmpty()) ? "Template: " + tmplName : "Active Template";
                    hasItems |= addVariableMenuItem(display, pasteSubMenu, itemCons, addListenerMethod, label, tmplText);
                }
            }

            if (!hasItems) {
                Object emptyItem = itemCons.newInstance(pasteSubMenu, SWT_PUSH);
                emptyItem.getClass().getMethod("setText", String.class).invoke(emptyItem, "No active SCMT variables");
                emptyItem.getClass().getMethod("setEnabled", boolean.class).invoke(emptyItem, false);
            }
        } catch (Throwable t) {
            log("Error populating Quick Paste items: " + t.getMessage());
        }
    }

    private static boolean addVariableMenuItem(final Object display, Object menu, Constructor<?> itemCons, Method addListenerMethod, String label, final String value) {
        if (value == null || value.trim().isEmpty()) return false;
        try {
            Object item = itemCons.newInstance(menu, SWT_PUSH);
            String displayVal = value.length() > 30 ? value.substring(0, 27) + "..." : value;
            String menuText = label + ": " + displayVal;
            item.getClass().getMethod("setText", String.class).invoke(item, menuText);

            Object listener = createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                @Override
                public void run() {
                    runOnDisplay(display, new Runnable() {
                        @Override
                        public void run() {
                            insertTextIntoFocusControl(display, value);
                        }
                    });
                }
            });
            addListenerMethod.invoke(item, SWT_Selection, listener);
            return true;
        } catch (Throwable ignored) {
            return false;
        }
    }

    private static Object createListenerProxy(ClassLoader cl, final Runnable action) throws Exception {
        Class<?> listenerClass = Class.forName("org.eclipse.swt.widgets.Listener");
        InvocationHandler handler = new InvocationHandler() {
            @Override
            public Object invoke(Object proxy, Method method, Object[] args) throws Throwable {
                if ("handleEvent".equals(method.getName())) {
                    action.run();
                }
                return null;
            }
        };
        return Proxy.newProxyInstance(cl, new Class<?>[] { listenerClass }, handler);
    }

    // ==========================================
    // Dialog Enhancements (Publish, Form Properties, Prompts)
    // ==========================================

    private static void enhanceDialogShell(final Object display, final Object shell) {
        if (shell == null || isDisposed(shell)) return;
        try {
            Method getDataMethod = shell.getClass().getMethod("getData", String.class);
            if (Boolean.TRUE.equals(getDataMethod.invoke(shell, "SoleraEnhanced"))) {
                return;
            }

            Method isVisibleMethod = shell.getClass().getMethod("isVisible");
            boolean visible = (Boolean) isVisibleMethod.invoke(shell);
            if (!visible) return;

            Method getTextMethod = shell.getClass().getMethod("getText");
            String title = (String) getTextMethod.invoke(shell);
            if (title == null) title = "";

            // 1. Check Publish / Revoke Dialog
            if (title.contains("Publish") || title.contains("Revoke")) {
                Object publishDlg = extractTargetObject(shell, "com.automate.formgen.dialogs.checkinout.PublishDlg", 0);
                enhancePublishDialog(display, shell, publishDlg);
                return;
            }

            // 2. Check Form Properties Dialog
            if (title.contains("Form Properties")) {
                Object formPropsDlg = extractTargetObject(shell, "com.automate.formgen.dialogs.FormPropertiesDlg", 0);
                enhanceFormPropertiesDialog(display, shell, formPropsDlg);
                return;
            }

            // 3. Check UDF Code & Prompts Dialog
            if (title.contains("UDF") || title.contains("Screen Prompts") || title.contains("Prompts")) {
                Object udfDlg = extractTargetObject(shell, "com.automate.formgen.dialogs.UdfDlg", 0);
                if (udfDlg == null && formGenInstance != null) {
                    try {
                        Field f = formGenInstance.getClass().getDeclaredField("dlgCode");
                        f.setAccessible(true);
                        udfDlg = f.get(formGenInstance);
                    } catch (Throwable ignored) {}
                }
                enhanceUdfDialog(display, shell, udfDlg);
                return;
            }
        } catch (Throwable t) {
            log("Error in enhanceDialogShell: " + t.getMessage());
        }
    }

    private static void enhancePublishDialog(final Object display, final Object shell, final Object publishDlg) {
        try {
            log("Enhancing PublishDlg with '⚡ Fill from Solera CMS' button...");
            Object btnPublish = null;
            if (publishDlg != null) {
                try {
                    Field btnPublishField = publishDlg.getClass().getDeclaredField("btnPublish");
                    btnPublishField.setAccessible(true);
                    btnPublish = btnPublishField.get(publishDlg);
                } catch (Throwable ignored) {}
            }
            if (btnPublish == null || isDisposed(btnPublish)) {
                btnPublish = findChildButton(shell, "Publish");
            }
            if (btnPublish == null || isDisposed(btnPublish)) {
                btnPublish = findChildButton(shell, "Revoke");
            }
            if (btnPublish == null || isDisposed(btnPublish)) return;

            Method getParentMethod = btnPublish.getClass().getMethod("getParent");
            Object buttonComposite = getParentMethod.invoke(btnPublish);
            if (buttonComposite == null) return;

            if (findChildButton(buttonComposite, "Fill from Solera") != null) return;

            // Expand numColumns of GridLayout
            try {
                Object layout = buttonComposite.getClass().getMethod("getLayout").invoke(buttonComposite);
                if (layout != null) {
                    Field nc = layout.getClass().getField("numColumns");
                    nc.setInt(layout, nc.getInt(layout) + 1);
                }
            } catch (Throwable ignored) {}

            Class<?> buttonClass = Class.forName("org.eclipse.swt.widgets.Button");
            Class<?> compositeClass = Class.forName("org.eclipse.swt.widgets.Composite");
            Constructor<?> btnCons = buttonClass.getConstructor(compositeClass, int.class);
            Object btnFill = btnCons.newInstance(buttonComposite, SWT_PUSH);

            btnFill.getClass().getMethod("setText", String.class).invoke(btnFill, "⚡ Fill from Solera CMS");
            try {
                btnFill.getClass().getMethod("setToolTipText", String.class).invoke(btnFill, "Auto-fill Authorized By, Dealer Code, Companies, and Comments from active SCMT case");
            } catch (Throwable ignored) {}

            Object listener = createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                @Override
                public void run() {
                    try {
                        refreshLiveVariablesSync();
                        synchronized (liveVariables) {
                            String serverId = liveVariables.get("ServerId");
                            String caseNumber = liveVariables.get("CaseNumber");
                            String primaryCo = liveVariables.get("NotableCompanyNumbers");
                            if (primaryCo == null || primaryCo.trim().isEmpty()) {
                                primaryCo = liveVariables.get("PrimaryCompanyNumber");
                            }
                            String contactName = liveVariables.get("ContactName");
                            String notes = liveVariables.get("Notes");
                            String tmplText = liveVariables.get("ActiveTemplateText");

                            log("Publish auto-fill: ServerId=" + serverId + ", ActiveTemplateText len=" + (tmplText != null ? tmplText.length() : -1)
                                + ", CaseNumber=" + caseNumber + ", Contact=" + contactName);

                            if (publishDlg != null) {
                                if (contactName != null && !contactName.trim().isEmpty()) {
                                    setWidgetText(publishDlg, "txtAuthorizedBy", contactName);
                                }
                                if (serverId != null && !serverId.trim().isEmpty()) {
                                    setWidgetText(publishDlg, "txtSingleDealerCode", serverId);
                                }
                                if (primaryCo != null && !primaryCo.trim().isEmpty()) {
                                    setWidgetText(publishDlg, "txtPublishSingleCompanies", primaryCo);
                                }

                                String comments = "";
                                if (tmplText != null && !tmplText.trim().isEmpty()) {
                                    comments = tmplText.trim();
                                } else {
                                    if (caseNumber != null && !caseNumber.trim().isEmpty()) comments = "Case #" + caseNumber;
                                    if (notes != null && !notes.trim().isEmpty()) comments += (comments.isEmpty() ? "" : " - ") + notes;
                                }
                                if (!comments.isEmpty()) {
                                    setWidgetText(publishDlg, "txtComments", comments);
                                }

                                try {
                                    Method m = publishDlg.getClass().getDeclaredMethod("updateSingleDealerName");
                                    m.setAccessible(true);
                                    m.invoke(publishDlg);
                                } catch (Throwable ignored) {}
                            }
                        }
                        log("Successfully auto-populated PublishDlg from Solera CMS.");
                    } catch (Throwable t) {
                        log("Error auto-populating PublishDlg: " + t.getMessage());
                    }
                }
            });

            Method addListenerMethod = buttonClass.getMethod("addListener", int.class, Class.forName("org.eclipse.swt.widgets.Listener"));
            addListenerMethod.invoke(btnFill, SWT_Selection, listener);

            buttonComposite.getClass().getMethod("layout", boolean.class, boolean.class).invoke(buttonComposite, true, true);
            shell.getClass().getMethod("layout", boolean.class, boolean.class).invoke(shell, true, true);

            shell.getClass().getMethod("setData", String.class, Object.class).invoke(shell, "SoleraEnhanced", Boolean.TRUE);
            log("PublishDlg successfully enhanced.");
        } catch (Throwable t) {
            log("Error enhancing PublishDlg: " + t.getMessage());
        }
    }

    private static void enhanceFormPropertiesDialog(final Object display, final Object shell, final Object formPropsDlg) {
        try {
            log("Enhancing FormPropertiesDlg with '🔄 Regenerate UUID' button...");

            // Look for btnOk or composite containing Ok/Cancel
            Object btnOk = null;
            if (formPropsDlg != null) {
                try {
                    Field btnOkField = formPropsDlg.getClass().getDeclaredField("btnOk");
                    btnOkField.setAccessible(true);
                    btnOk = btnOkField.get(formPropsDlg);
                } catch (Throwable ignored) {}
            }
            if (btnOk == null || isDisposed(btnOk)) {
                btnOk = findChildButton(shell, "Ok");
            }
            if (btnOk == null || isDisposed(btnOk)) {
                btnOk = findChildButton(shell, "&Ok");
            }
            if (btnOk == null || isDisposed(btnOk)) return;

            Method getParentMethod = btnOk.getClass().getMethod("getParent");
            Object buttonComposite = getParentMethod.invoke(btnOk);
            if (buttonComposite == null) return;

            if (findChildButton(buttonComposite, "Regenerate UUID") != null) return;

            // Expand numColumns of GridLayout
            try {
                Object layout = buttonComposite.getClass().getMethod("getLayout").invoke(buttonComposite);
                if (layout != null) {
                    Field nc = layout.getClass().getField("numColumns");
                    nc.setInt(layout, nc.getInt(layout) + 1);
                }
            } catch (Throwable ignored) {}

            Class<?> buttonClass = Class.forName("org.eclipse.swt.widgets.Button");
            Class<?> compositeClass = Class.forName("org.eclipse.swt.widgets.Composite");
            Constructor<?> btnCons = buttonClass.getConstructor(compositeClass, int.class);
            Object btnRegen = btnCons.newInstance(buttonComposite, SWT_PUSH);

            btnRegen.getClass().getMethod("setText", String.class).invoke(btnRegen, "🔄 Regenerate UUID (Clone)");
            try {
                btnRegen.getClass().getMethod("setToolTipText", String.class).invoke(btnRegen, "Assigns a fresh UUID so this form can be published as an independent form clone");
            } catch (Throwable ignored) {}

            Object listener = createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                @Override
                public void run() {
                    try {
                        Object formdef = null;
                        if (formPropsDlg != null) {
                            try {
                                Field f = formPropsDlg.getClass().getDeclaredField("formdef");
                                f.setAccessible(true);
                                formdef = f.get(formPropsDlg);
                            } catch (Throwable ignored) {}
                        }
                        if (formdef == null) {
                            formdef = getActiveFormDef();
                        }
                        if (formdef == null) {
                            log("Could not resolve FormDef for UUID regeneration.");
                            return;
                        }

                        UUID newUuid = UUID.randomUUID();
                        Method setUuidMethod = formdef.getClass().getMethod("setPublishedUUID", UUID.class);
                        setUuidMethod.invoke(formdef, newUuid);

                        Object lblPublishID = null;
                        if (formPropsDlg != null) {
                            try {
                                Field f = formPropsDlg.getClass().getDeclaredField("lblPublishID");
                                f.setAccessible(true);
                                lblPublishID = f.get(formPropsDlg);
                            } catch (Throwable ignored) {}
                        }
                        if (lblPublishID == null || isDisposed(lblPublishID)) {
                            lblPublishID = findChildControl(shell, "AMLabel");
                        }
                        if (lblPublishID != null && !isDisposed(lblPublishID)) {
                            lblPublishID.getClass().getMethod("setText", String.class).invoke(lblPublishID, newUuid.toString());
                        }

                        log("Successfully regenerated UUID to: " + newUuid);
                    } catch (Throwable t) {
                        log("Error regenerating UUID: " + t.getMessage());
                    }
                }
            });

            Method addListenerMethod = buttonClass.getMethod("addListener", int.class, Class.forName("org.eclipse.swt.widgets.Listener"));
            addListenerMethod.invoke(btnRegen, SWT_Selection, listener);

            buttonComposite.getClass().getMethod("layout", boolean.class, boolean.class).invoke(buttonComposite, true, true);
            shell.getClass().getMethod("layout", boolean.class, boolean.class).invoke(shell, true, true);

            shell.getClass().getMethod("setData", String.class, Object.class).invoke(shell, "SoleraEnhanced", Boolean.TRUE);
            log("FormPropertiesDlg successfully enhanced.");
        } catch (Throwable t) {
            log("Error enhancing FormPropertiesDlg: " + t.getMessage());
        }
    }

    private static void enhanceUdfDialog(final Object display, final Object shell, final Object udfDlg) {
        try {
            log("Enhancing UdfDlg with '🛠 Send to FormGen Utilities' button...");

            Object compButtons = null;
            if (udfDlg != null) {
                try {
                    Field compButtonsField = udfDlg.getClass().getDeclaredField("compButtons");
                    compButtonsField.setAccessible(true);
                    compButtons = compButtonsField.get(udfDlg);
                } catch (Throwable ignored) {}
            }
            if (compButtons == null || isDisposed(compButtons)) {
                Object btnApply = findChildButton(shell, "Apply");
                if (btnApply != null && !isDisposed(btnApply)) {
                    compButtons = btnApply.getClass().getMethod("getParent").invoke(btnApply);
                }
            }
            if (compButtons == null || isDisposed(compButtons)) {
                log("Could not find compButtons in UdfDlg.");
                return;
            }

            if (findChildButton(compButtons, "FormGen Utilities") != null) return;

            // Expand numColumns of GridLayout
            try {
                Object layout = compButtons.getClass().getMethod("getLayout").invoke(compButtons);
                if (layout != null) {
                    Field nc = layout.getClass().getField("numColumns");
                    nc.setInt(layout, nc.getInt(layout) + 1);
                }
            } catch (Throwable ignored) {}

            Class<?> buttonClass = Class.forName("org.eclipse.swt.widgets.Button");
            Class<?> compositeClass = Class.forName("org.eclipse.swt.widgets.Composite");
            Constructor<?> btnCons = buttonClass.getConstructor(compositeClass, int.class);
            Object btnOpenUtils = btnCons.newInstance(compButtons, SWT_PUSH);

            btnOpenUtils.getClass().getMethod("setText", String.class).invoke(btnOpenUtils, "🛠 Send to FormGen Utilities");
            try {
                btnOpenUtils.getClass().getMethod("setToolTipText", String.class).invoke(btnOpenUtils, "Opens the active form in SCMT FormGen Utilities to duplicate prompts, parse fields, and align layouts");
            } catch (Throwable ignored) {}

            Object listener = createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                @Override
                public void run() {
                    notifyScmtOpenFormgenUtils(getActiveFormFilePath());
                }
            });

            Method addListenerMethod = buttonClass.getMethod("addListener", int.class, Class.forName("org.eclipse.swt.widgets.Listener"));
            addListenerMethod.invoke(btnOpenUtils, SWT_Selection, listener);

            compButtons.getClass().getMethod("layout", boolean.class, boolean.class).invoke(compButtons, true, true);
            shell.getClass().getMethod("layout", boolean.class, boolean.class).invoke(shell, true, true);

            shell.getClass().getMethod("setData", String.class, Object.class).invoke(shell, "SoleraEnhanced", Boolean.TRUE);
            log("UdfDlg successfully enhanced.");
        } catch (Throwable t) {
            log("Error enhancing UdfDlg: " + t.getMessage());
        }
    }

    public static Object getActiveFormDef() {
        if (!discoverFormGen()) return null;
        try {
            Field fvField = formGenInstance.getClass().getDeclaredField("formViewer");
            fvField.setAccessible(true);
            Object formViewer = fvField.get(formGenInstance);
            if (formViewer != null) {
                Method getFormDefMethod = formViewer.getClass().getMethod("getFormDef");
                return getFormDefMethod.invoke(formViewer);
            }
        } catch (Throwable ignored) {}
        return null;
    }

    private static void setWidgetText(Object target, String fieldName, String text) {
        try {
            Field f = target.getClass().getDeclaredField(fieldName);
            f.setAccessible(true);
            Object widget = f.get(target);
            if (widget != null && !isDisposed(widget)) {
                Method m = widget.getClass().getMethod("setText", String.class);
                m.invoke(widget, text);
            }
        } catch (Throwable ignored) {}
    }

    // ==========================================
    // Focus Control Text Helpers
    // ==========================================

    public static String getSelectedTextFromFocusControl(Object display) {
        if (display == null) return "";
        try {
            Method getFocusMethod = display.getClass().getMethod("getFocusControl");
            Object control = getFocusMethod.invoke(display);
            if (control == null) return "";

            try {
                Method m = control.getClass().getMethod("getSelectionText");
                String text = (String) m.invoke(control);
                if (text != null && !text.trim().isEmpty()) return text;
            } catch (Throwable ignored) {}

            try {
                Method m = control.getClass().getMethod("getText");
                String text = (String) m.invoke(control);
                if (text != null && !text.trim().isEmpty()) return text;
            } catch (Throwable ignored) {}
        } catch (Throwable t) {
            log("Error getting text from focus control: " + t.getMessage());
        }
        return "";
    }

    public static void insertTextIntoFocusControl(Object display, String text) {
        if (display == null || text == null || text.isEmpty()) return;
        try {
            Method getFocusMethod = display.getClass().getMethod("getFocusControl");
            Object control = getFocusMethod.invoke(display);
            if (control == null) return;

            try {
                Method insertMethod = control.getClass().getMethod("insert", String.class);
                insertMethod.invoke(control, text);
                return;
            } catch (Throwable ignored) {}

            try {
                Method pasteMethod = control.getClass().getMethod("paste");
                java.awt.datatransfer.StringSelection selection = new java.awt.datatransfer.StringSelection(text);
                java.awt.Toolkit.getDefaultToolkit().getSystemClipboard().setContents(selection, selection);
                pasteMethod.invoke(control);
            } catch (Throwable ignored) {}
        } catch (Throwable t) {
            log("Error inserting text into focus control: " + t.getMessage());
        }
    }

    private static String getClipboardText() {
        try {
            java.awt.datatransfer.Transferable t = java.awt.Toolkit.getDefaultToolkit().getSystemClipboard().getContents(null);
            if (t != null && t.isDataFlavorSupported(java.awt.datatransfer.DataFlavor.stringFlavor)) {
                return (String) t.getTransferData(java.awt.datatransfer.DataFlavor.stringFlavor);
            }
        } catch (Throwable ignored) {}
        return "";
    }

    // ==========================================
    // FormGen File Discovery Helpers
    // ==========================================

    public static String getActiveFormFilePath() {
        if (formGenInstance != null) {
            try {
                Method m = formGenInstance.getClass().getDeclaredMethod("getCurrentFormgenFile");
                m.setAccessible(true);
                Object res = m.invoke(formGenInstance);
                if (res instanceof File && ((File) res).exists()) return ((File) res).getAbsolutePath();
                if (res instanceof String && new File((String) res).exists()) return (String) res;
            } catch (Throwable ignored) {}

            try {
                Method m = formGenInstance.getClass().getDeclaredMethod("getFormDef");
                m.setAccessible(true);
                Object formDef = m.invoke(formGenInstance);
                if (formDef != null) {
                    for (Method dm : formDef.getClass().getDeclaredMethods()) {
                        String name = dm.getName().toLowerCase();
                        if (name.contains("file") || name.contains("path")) {
                            try {
                                dm.setAccessible(true);
                                Object res = dm.invoke(formDef);
                                if (res instanceof File && ((File) res).exists()) return ((File) res).getAbsolutePath();
                                if (res instanceof String && new File((String) res).exists()) return (String) res;
                            } catch (Throwable ignored) {}
                        }
                    }
                }
            } catch (Throwable ignored) {}

            try {
                for (Field f : formGenInstance.getClass().getDeclaredFields()) {
                    String name = f.getName().toLowerCase();
                    if (name.contains("formgenfile") || name.contains("activefile") || name.contains("currentfile")) {
                        try {
                            f.setAccessible(true);
                            Object val = f.get(formGenInstance);
                            if (val instanceof File && ((File) val).exists()) return ((File) val).getAbsolutePath();
                            if (val instanceof String && new File((String) val).exists()) return (String) val;
                        } catch (Throwable ignored) {}
                    }
                }
            } catch (Throwable ignored) {}
        }

        if (mainShell != null && !isDisposed(mainShell)) {
            try {
                String title = (String) mainShell.getClass().getMethod("getText").invoke(mainShell);
                if (title != null) {
                    String extracted = extractPathFromText(title);
                    if (extracted != null) return extracted;
                }
            } catch (Throwable ignored) {}
        }

        if (lastOpenedFilePath != null && new File(lastOpenedFilePath).exists()) {
            return lastOpenedFilePath;
        }

        return "";
    }

    private static String extractPathFromText(String text) {
        if (text == null) return null;
        Pattern p = Pattern.compile("([a-zA-Z]:\\\\[^<>:\"/\\\\|?*\\r\\n]+\\.(?:formgen|fga))", Pattern.CASE_INSENSITIVE);
        Matcher m = p.matcher(text);
        if (m.find()) {
            String candidate = m.group(1).trim();
            if (new File(candidate).exists()) return candidate;
        }
        return null;
    }

    // ==========================================
    // SCMT Bidirectional IPC Communication
    // ==========================================

    public static void notifyScmtOpenFormgenUtils(String filePath) {
        Map<String, String> payload = new LinkedHashMap<String, String>();
        if (filePath != null && !filePath.trim().isEmpty()) {
            payload.put("filePath", filePath);
        }
        sendIpcMessageToScmt("OpenFormgen", payload, null);
    }

    public static void notifyScmtParseCode(String codeText) {
        Map<String, String> payload = new LinkedHashMap<String, String>();
        if (codeText != null && !codeText.trim().isEmpty()) {
            payload.put("code", codeText);
        }
        sendIpcMessageToScmt("ParseCodeBlock", payload, codeText);
    }

    public static boolean sendIpcMessageToScmt(String action, Map<String, String> payload, String rawData) {
        try {
            StringBuilder json = new StringBuilder();
            json.append("{\"target\":\"SoleraCMS\",\"action\":\"").append(escapeJson(action)).append("\",\"payload\":{");
            if (payload != null && !payload.isEmpty()) {
                boolean first = true;
                for (Map.Entry<String, String> e : payload.entrySet()) {
                    if (!first) json.append(",");
                    json.append("\"").append(escapeJson(e.getKey())).append("\":\"").append(escapeJson(e.getValue())).append("\"");
                    first = false;
                }
            }
            json.append("}");
            if (rawData != null) {
                json.append(",\"rawData\":\"").append(escapeJson(rawData)).append("\"");
            }
            json.append("}\n");

            RandomAccessFile pipe = null;
            try {
                pipe = new RandomAccessFile("\\\\.\\pipe\\SoleraCMS_IPC", "rw");
                pipe.write(json.toString().getBytes("UTF-8"));

                ByteArrayOutputStream bos = new ByteArrayOutputStream();
                int b;
                while ((b = pipe.read()) != -1) {
                    if (b == '\n') break;
                    if (b != '\r') bos.write(b);
                }
                String response = new String(bos.toByteArray(), "UTF-8");
                log("IPC message '" + action + "' successfully sent to SoleraCMS. Response: " + response);
                return true;
            } catch (Throwable t) {
                log("Named pipe IPC send failed for action '" + action + "': " + t.getMessage());
            } finally {
                if (pipe != null) {
                    try { pipe.close(); } catch (Throwable ignored) {}
                }
            }
        } catch (Throwable t) {
            log("IPC JSON creation error: " + t.getMessage());
        }

        return launchScmtFallback(action, payload, rawData);
    }

    private static boolean launchScmtFallback(String action, Map<String, String> payload, String rawData) {
        try {
            String exePath = findScmtExecutable();
            if (exePath == null) {
                log("Could not locate Solera CMS executable for fallback launch.");
                return false;
            }

            List<String> cmd = new ArrayList<String>();
            cmd.add(exePath);

            if ("OpenFormgen".equalsIgnoreCase(action)) {
                String fp = payload.get("filePath");
                if (fp != null && !fp.isEmpty()) {
                    cmd.add("--open-formgen");
                    cmd.add(fp);
                }
            } else if ("ParseCodeBlock".equalsIgnoreCase(action)) {
                String code = rawData != null ? rawData : payload.get("code");
                if (code != null && !code.isEmpty()) {
                    cmd.add("--parse-code");
                    cmd.add(code);
                }
            }

            new ProcessBuilder(cmd).start();
            log("Launched SCMT fallback: " + cmd);
            return true;
        } catch (Throwable t) {
            log("Failed to launch SCMT fallback: " + t.getMessage());
            return false;
        }
    }

    private static String findScmtExecutable() {
        if (scmtPath != null && new File(scmtPath).exists()) {
            return scmtPath;
        }

        // 1. Query registered SCMT context menu command in registry
        try {
            Process process = Runtime.getRuntime().exec("reg query HKCU\\Software\\Classes\\formgenfile\\shell\\OpenInSoleraUtils\\command /ve");
            BufferedReader reader = new BufferedReader(new InputStreamReader(process.getInputStream()));
            String line;
            while ((line = reader.readLine()) != null) {
                if (line.contains("REG_SZ")) {
                    String val = line.substring(line.indexOf("REG_SZ") + 6).trim();
                    if (val.startsWith("\"")) {
                        int endQuote = val.indexOf("\"", 1);
                        if (endQuote > 1) {
                            String exe = val.substring(1, endQuote);
                            if (new File(exe).exists()) {
                                scmtPath = exe;
                                return exe;
                            }
                        }
                    }
                }
            }
        } catch (Throwable ignored) {}

        // 2. Candidate developer and installed paths
        String userName = System.getProperty("user.name");
        String localAppData = System.getenv("LOCALAPPDATA");
        String[] candidatePaths = new String[] {
            "C:\\Users\\" + userName + "\\source\\repos\\TheHeartOfFire\\Solera-Case-Management-System\\AMFormsCST.Desktop\\bin\\Debug\\net9.0-windows\\AMFormsCST.Desktop.exe",
            "D:\\source\\TheHeartOfFire\\Solera-Case-Management-System\\AMFormsCST.Desktop\\bin\\Debug\\net9.0-windows10.0.19041.0\\AMFormsCST.Desktop.exe",
            "D:\\source\\TheHeartOfFire\\Solera-Case-Management-System\\AMFormsCST.Desktop\\bin\\Debug\\net9.0-windows\\AMFormsCST.Desktop.exe",
            localAppData != null ? localAppData + "\\SoleraCaseManagementTool\\current\\AMFormsCST.Desktop.exe" : null,
            localAppData != null ? localAppData + "\\SoleraCaseManagementTool\\current\\SoleraCaseManagementTool.exe" : null,
            localAppData != null ? localAppData + "\\AMFormsCST.Desktop\\current\\AMFormsCST.Desktop.exe" : null
        };

        for (String p : candidatePaths) {
            if (p != null && new File(p).exists()) {
                scmtPath = p;
                return p;
            }
        }

        return null;
    }

    public static void refreshLiveVariablesSync() {
        RandomAccessFile pipe = null;
        try {
            pipe = new RandomAccessFile("\\\\.\\pipe\\SoleraCMS_IPC", "rw");
            pipe.write("{\"target\":\"SoleraCMS\",\"action\":\"GetLiveVariables\"}\n".getBytes("UTF-8"));
            ByteArrayOutputStream bos = new ByteArrayOutputStream();
            int b;
            while ((b = pipe.read()) != -1) {
                if (b == '\n') break;
                if (b != '\r') bos.write(b);
            }
            String line = new String(bos.toByteArray(), "UTF-8");
            if (!line.trim().isEmpty()) {
                updateLiveVariablesFromJson(line);
            }
        } catch (Throwable t) {
            log("Error during synchronous variable refresh: " + t.getMessage());
        } finally {
            if (pipe != null) {
                try { pipe.close(); } catch (Throwable ignored) {}
            }
        }
    }

    public static void refreshLiveVariablesAsync() {
        new Thread(new Runnable() {
            @Override
            public void run() {
                refreshLiveVariablesSync();
            }
        }, "FormGenAgent-VarSync").start();
    }

    public static void updateLiveVariablesFromJson(String json) {
        if (json == null || json.trim().isEmpty()) return;
        try {
            int i = 0, len = json.length();
            synchronized (liveVariables) {
                while (i < len) {
                    // Find start of key
                    int keyStart = json.indexOf('"', i);
                    if (keyStart == -1) break;
                    int keyEnd = json.indexOf('"', keyStart + 1);
                    if (keyEnd == -1) break;
                    String key = json.substring(keyStart + 1, keyEnd);

                    // Find ':' separator
                    int colon = json.indexOf(':', keyEnd + 1);
                    if (colon == -1) break;

                    // Skip whitespace after colon
                    int valStart = colon + 1;
                    while (valStart < len && Character.isWhitespace(json.charAt(valStart))) {
                        valStart++;
                    }
                    if (valStart >= len) break;

                    char firstChar = json.charAt(valStart);
                    if (firstChar == '{' || firstChar == '[') {
                        // Enter nested object or array without consuming inner keys
                        i = valStart + 1;
                        continue;
                    }

                    if (firstChar == '"') {
                        // String value: scan until unescaped closing quote
                        StringBuilder valSb = new StringBuilder();
                        int idx = valStart + 1;
                        while (idx < len) {
                            char c = json.charAt(idx);
                            if (c == '\\' && idx + 1 < len) {
                                valSb.append(c);
                                valSb.append(json.charAt(idx + 1));
                                idx += 2;
                            } else if (c == '"') {
                                idx++;
                                break;
                            } else {
                                valSb.append(c);
                                idx++;
                            }
                        }
                        String val = unescapeJson(valSb.toString());
                        liveVariables.put(key, val);
                        i = idx;
                    } else {
                        // Primitive value (true, false, number, null)
                        int nextComma = json.indexOf(',', valStart);
                        int nextBrace = json.indexOf('}', valStart);
                        int nextBracket = json.indexOf(']', valStart);
                        int end = len;
                        if (nextComma != -1) end = Math.min(end, nextComma);
                        if (nextBrace != -1) end = Math.min(end, nextBrace);
                        if (nextBracket != -1) end = Math.min(end, nextBracket);
                        String val = json.substring(valStart, end).trim();
                        liveVariables.put(key, val);
                        i = end;
                    }
                }
            }
        } catch (Throwable t) {
            log("Error parsing live variables JSON: " + t.getMessage());
        }
    }

    private static String escapeJson(String s) {
        if (s == null) return "";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            switch (c) {
                case '"': sb.append("\\\""); break;
                case '\\': sb.append("\\\\"); break;
                case '\b': sb.append("\\b"); break;
                case '\f': sb.append("\\f"); break;
                case '\n': sb.append("\\n"); break;
                case '\r': sb.append("\\r"); break;
                case '\t': sb.append("\\t"); break;
                default:
                    if (c < ' ') {
                        String hex = Integer.toHexString(c);
                        sb.append("\\u");
                        for (int k = 0; k < 4 - hex.length(); k++) sb.append('0');
                        sb.append(hex);
                    } else {
                        sb.append(c);
                    }
            }
        }
        return sb.toString();
    }

    private static String unescapeJson(String s) {
        if (s == null) return "";
        StringBuilder sb = new StringBuilder();
        int i = 0;
        int len = s.length();
        while (i < len) {
            char c = s.charAt(i++);
            if (c == '\\' && i < len) {
                char c2 = s.charAt(i++);
                switch (c2) {
                    case '"': sb.append('"'); break;
                    case '\\': sb.append('\\'); break;
                    case '/': sb.append('/'); break;
                    case 'b': sb.append('\b'); break;
                    case 'f': sb.append('\f'); break;
                    case 'n': sb.append('\n'); break;
                    case 'r': sb.append('\r'); break;
                    case 't': sb.append('\t'); break;
                    case 'u':
                        if (i + 4 <= len) {
                            String hex = s.substring(i, i + 4);
                            sb.append((char) Integer.parseInt(hex, 16));
                            i += 4;
                        }
                        break;
                    default:
                        sb.append(c2);
                }
            } else {
                sb.append(c);
            }
        }
        return sb.toString();
    }
}
