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
    private static volatile boolean transformerInstalled = false;
    private static volatile int lastUdfTabIndex = -1;
    private static volatile int lastMenuDetectX = -1;
    private static volatile int lastMenuDetectY = -1;
    private static volatile long lastConfigModified = 0L;
    private static volatile boolean sessionRestoreCompleted = false;
    private static final String PORT_FILE = System.getProperty("java.io.tmpdir") + File.separator + "formgen_agent.port";
    private static final String LOG_FILE = System.getProperty("java.io.tmpdir") + File.separator + "formgen_agent.log";
    private static final String CONFIG_FILE = System.getProperty("java.io.tmpdir") + File.separator + "formgen_agent.properties";
    private static final String SESSION_FORMS_FILE = System.getProperty("java.io.tmpdir") + File.separator + "formgen_session_forms.txt";

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

    public static void reloadConfigIfChanged() {
        try {
            File cf = new File(CONFIG_FILE);
            if (!cf.exists()) return;
            long mod = cf.lastModified();
            if (mod == lastConfigModified) return;
            lastConfigModified = mod;

            BufferedReader br = new BufferedReader(new InputStreamReader(new FileInputStream(cf), "UTF-8"));
            String line;
            synchronized (liveVariables) {
                while ((line = br.readLine()) != null) {
                    line = line.trim();
                    if (line.isEmpty() || line.startsWith("#")) continue;
                    int eq = line.indexOf('=');
                    if (eq > 0) {
                        String k = line.substring(0, eq).trim();
                        String v = line.substring(eq + 1).trim();
                        liveVariables.put(k, v);
                    }
                }
            }
            br.close();
        } catch (Throwable ignored) {}
    }

    public static boolean isFeatureEnabled(String featureKey) {
        if (featureKey == null || featureKey.isEmpty()) return true;
        reloadConfigIfChanged();
        synchronized (liveVariables) {
            String val = liveVariables.get(featureKey);
            if (val == null || val.trim().isEmpty()) return true;
            return !"false".equalsIgnoreCase(val.trim()) && !"0".equals(val.trim());
        }
    }

    public static void premain(String agentArgs, Instrumentation inst) {
        init(agentArgs, false, inst);
    }

    public static void agentmain(String agentArgs, Instrumentation inst) {
        init(agentArgs, true, inst);
    }

    private static synchronized void init(final String agentArgs, boolean isDynamicAttach, Instrumentation inst) {
        log("Initializing agent (args: " + agentArgs + ", dynamic: " + isDynamicAttach + ")...");
        reloadConfigIfChanged();

        String sysPropFile = System.getProperty("formgen.file");
        String fileToOpen = cleanFilePath(sysPropFile != null && !sysPropFile.trim().isEmpty() ? sysPropFile : agentArgs);

        if (!isDynamicAttach && tryForwardToExistingInstance(fileToOpen)) {
            log("Forwarded to existing instance. Exiting secondary JVM.");
            System.exit(0);
            return;
        }

        final List<String> previousSessionForms = readSessionFormsFile();

        try {
            new ClientHandler(null);
        } catch (Throwable ignored) {}

        installClassTransformer(inst);
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

        if (!previousSessionForms.isEmpty()) {
            restoreSessionFormsWithRetry(previousSessionForms, fileToOpen, 120);
        } else {
            sessionRestoreCompleted = true;
        }
    }

    private static synchronized void installClassTransformer(Instrumentation inst) {
        if (inst == null || transformerInstalled) return;
        transformerInstalled = true;
        try {
            inst.addTransformer(new java.lang.instrument.ClassFileTransformer() {
                @Override
                public byte[] transform(ClassLoader loader, String className, Class<?> classBeingRedefined,
                                        java.security.ProtectionDomain protectionDomain, byte[] classfileBuffer) {
                    if (className == null) return null;
                    if (!"com/automate/formgen/dialogs/UdfDlg".equals(className) &&
                        !"com/automate/formgen/NewFormGen".equals(className)) {
                        return null;
                    }
                    try {
                        ClassLoader cl = loader != null ? loader : ClassLoader.getSystemClassLoader();
                        Class<?> poolClass = Class.forName("javassist.ClassPool", true, cl);
                        Object pool = poolClass.getMethod("getDefault").invoke(null);
                        try {
                            Class<?> loaderClassPathClass = Class.forName("javassist.LoaderClassPath", true, cl);
                            Object lcp = loaderClassPathClass.getConstructor(ClassLoader.class).newInstance(cl);
                            Class<?> classPathInterface = Class.forName("javassist.ClassPath", true, cl);
                            poolClass.getMethod("insertClassPath", classPathInterface).invoke(pool, lcp);
                        } catch (Throwable ignored) {}

                        Method makeClassMethod = poolClass.getMethod("makeClass", InputStream.class);
                        Object ctClass = makeClassMethod.invoke(pool, new ByteArrayInputStream(classfileBuffer));
                        try {
                            Method stopPruning = ctClass.getClass().getMethod("stopPruning", boolean.class);
                            stopPruning.invoke(ctClass, true);
                        } catch (Throwable ignored) {}
                        try {
                            Boolean isFrozen = (Boolean) ctClass.getClass().getMethod("isFrozen").invoke(ctClass);
                            if (Boolean.TRUE.equals(isFrozen)) {
                                ctClass.getClass().getMethod("defrost").invoke(ctClass);
                            }
                        } catch (Throwable ignored) {}

                        Method getDeclaredMethods = ctClass.getClass().getMethod("getDeclaredMethods");
                        Object[] methods = (Object[]) getDeclaredMethods.invoke(ctClass);

                        if ("com/automate/formgen/dialogs/UdfDlg".equals(className) && methods != null) {
                            for (Object m : methods) {
                                String mName = (String) m.getClass().getMethod("getName").invoke(m);
                                if ("loadCode".equals(mName)) {
                                    m.getClass().getMethod("insertBefore", String.class)
                                        .invoke(m, "com.solera.formgen.FormGenAgent.onBeforeUdfLoadCode($0);");
                                    m.getClass().getMethod("insertAfter", String.class)
                                        .invoke(m, "com.solera.formgen.FormGenAgent.onAfterUdfLoadCode($0);");
                                    log("Instrumented UdfDlg.loadCode to preserve selected tab.");
                                }
                            }
                        } else if ("com/automate/formgen/NewFormGen".equals(className) && methods != null) {
                            for (Object m : methods) {
                                String mName = (String) m.getClass().getMethod("getName").invoke(m);
                                if ("openFormgenFile".equals(mName)) {
                                    Object[] paramTypes = (Object[]) m.getClass().getMethod("getParameterTypes").invoke(m);
                                    if (paramTypes != null && paramTypes.length == 3) {
                                        m.getClass().getMethod("insertBefore", String.class)
                                            .invoke(m, "if (com.solera.formgen.FormGenAgent.onPreOpenFormgenFile($0, $1)) { return true; }");
                                        log("Instrumented NewFormGen.openFormgenFile to focus existing tab with toast notification.");
                                    }
                                }
                            }
                        }

                        byte[] transformed = (byte[]) ctClass.getClass().getMethod("toBytecode").invoke(ctClass);
                        try {
                            ctClass.getClass().getMethod("detach").invoke(ctClass);
                        } catch (Throwable ignored) {}
                        return transformed;
                    } catch (Throwable t) {
                        log("Note: Javassist transform skipped for " + className + " (using SWT/reflection fallback): " + t.getMessage());
                        return null;
                    }
                }
            }, true);

            if (inst.isRetransformClassesSupported()) {
                for (Class<?> loaded : inst.getAllLoadedClasses()) {
                    if (loaded != null) {
                        String name = loaded.getName();
                        if ("com.automate.formgen.dialogs.UdfDlg".equals(name) ||
                            "com.automate.formgen.NewFormGen".equals(name)) {
                            try {
                                inst.retransformClasses(loaded);
                            } catch (Throwable ignored) {}
                        }
                    }
                }
            }
        } catch (Throwable t) {
            log("Error installing ClassFileTransformer: " + t.getMessage());
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

    private static List<String> readSessionFormsFile() {
        List<String> list = new ArrayList<String>();
        try {
            File f = new File(SESSION_FORMS_FILE);
            if (!f.exists()) return list;
            BufferedReader br = new BufferedReader(new InputStreamReader(new FileInputStream(f), "UTF-8"));
            String line;
            while ((line = br.readLine()) != null) {
                String clean = cleanFilePath(line);
                if (!clean.isEmpty() && new File(clean).exists() && !list.contains(clean)) {
                    list.add(clean);
                }
            }
            br.close();
        } catch (Throwable ignored) {}
        return list;
    }

    private static void restoreSessionFormsWithRetry(final List<String> formPaths, final String alreadyOpeningPath, final int retriesLeft) {
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
                            restoreSessionFormsWithRetry(formPaths, alreadyOpeningPath, retriesLeft - 1);
                        } else {
                            sessionRestoreCompleted = true;
                        }
                        return;
                    }

                    runOnDisplay(display, new Runnable() {
                        @Override
                        public void run() {
                            if (discoverFormGen()) {
                                try {
                                    if (isFeatureEnabled("FormGenAutoRestoreForms")) {
                                        for (String path : formPaths) {
                                            if (path == null || path.isEmpty()) continue;
                                            if (alreadyOpeningPath != null && path.equalsIgnoreCase(alreadyOpeningPath)) continue;
                                            File f = new File(path);
                                            if (!f.exists()) continue;
                                            if (focusExistingTabIfOpen(f, false)) continue;
                                            try {
                                                Method openMethod = formGenInstance.getClass().getDeclaredMethod("openFormgenFile", File.class, boolean.class);
                                                openMethod.setAccessible(true);
                                                openMethod.invoke(formGenInstance, f, false);
                                                log("Auto-restored session form: " + f.getAbsolutePath());
                                            } catch (Throwable openErr) {
                                                log("Error auto-restoring form " + path + ": " + openErr.getMessage());
                                            }
                                        }
                                    }
                                } finally {
                                    sessionRestoreCompleted = true;
                                }
                            } else if (retriesLeft > 0) {
                                Object d = getDisplay();
                                if (d != null && !isDisposed(d)) {
                                    runTimer(d, 500, new Runnable() {
                                        @Override
                                        public void run() {
                                            restoreSessionFormsWithRetry(formPaths, alreadyOpeningPath, retriesLeft - 1);
                                        }
                                    });
                                } else {
                                    sessionRestoreCompleted = true;
                                }
                            } else {
                                sessionRestoreCompleted = true;
                            }
                        }
                    });
                } catch (Throwable t) {
                    sessionRestoreCompleted = true;
                    log("Error in restoreSessionFormsWithRetry: " + t.getMessage());
                }
            }
        }, "FormGenAgent-SessionRestore").start();
    }

    public static List<String> getOpenFormFilePathsOnUiThread() {
        List<String> paths = new ArrayList<String>();
        try {
            discoverFormGen();
            if (formGenInstance == null) return paths;
            Field f = formGenInstance.getClass().getDeclaredField("mainTabFolder");
            f.setAccessible(true);
            Object mainTabFolder = f.get(formGenInstance);
            if (mainTabFolder == null || isDisposed(mainTabFolder)) return paths;

            Method getItemsMethod = mainTabFolder.getClass().getMethod("getItems");
            Object[] tabItems = (Object[]) getItemsMethod.invoke(mainTabFolder);
            if (tabItems == null) return paths;

            for (Object tabItem : tabItems) {
                if (tabItem == null || isDisposed(tabItem)) continue;
                try {
                    Object viewer = tabItem.getClass().getMethod("getControl").invoke(tabItem);
                    if (viewer == null || isDisposed(viewer)) continue;
                    File curFile = (File) viewer.getClass().getMethod("getCurrentFormgenFile").invoke(viewer);
                    if (curFile != null && curFile.exists()) {
                        String abs = curFile.getAbsolutePath();
                        if (!paths.contains(abs)) {
                            paths.add(abs);
                        }
                    }
                } catch (Throwable ignored) {}
            }
        } catch (Throwable ignored) {}
        return paths;
    }

    public static List<String> getOpenFormFilePaths() {
        final List<String> result = new ArrayList<String>();
        Object display = getDisplay();
        if (display == null || isDisposed(display)) return result;
        try {
            Method getThreadMethod = display.getClass().getMethod("getThread");
            Object dispThread = getThreadMethod.invoke(display);
            if (Thread.currentThread() == dispThread) {
                return getOpenFormFilePathsOnUiThread();
            }
            Method syncExecMethod = display.getClass().getMethod("syncExec", Runnable.class);
            syncExecMethod.invoke(display, new Runnable() {
                @Override
                public void run() {
                    result.addAll(getOpenFormFilePathsOnUiThread());
                }
            });
        } catch (Throwable ignored) {}
        return result;
    }

    public static void saveOpenFormsSnapshotOnUiThread() {
        if (!isFeatureEnabled("FormGenAutoRestoreForms")) {
            try {
                File f = new File(SESSION_FORMS_FILE);
                if (f.exists()) f.delete();
            } catch (Throwable ignored) {}
            return;
        }
        if (!sessionRestoreCompleted) return;
        try {
            if (mainShell == null || isDisposed(mainShell)) return;
            Boolean vis = (Boolean) mainShell.getClass().getMethod("isVisible").invoke(mainShell);
            if (!Boolean.TRUE.equals(vis)) return;

            List<String> paths = getOpenFormFilePathsOnUiThread();
            FileWriter fw = new FileWriter(SESSION_FORMS_FILE, false);
            try {
                for (String p : paths) {
                    fw.write(p + "\n");
                }
            } finally {
                fw.close();
            }
        } catch (Throwable ignored) {}
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

    public static Object getUdfDlg() {
        if (formGenInstance == null) return null;
        try {
            Field dlgCodeField = formGenInstance.getClass().getDeclaredField("dlgCode");
            dlgCodeField.setAccessible(true);
            return dlgCodeField.get(formGenInstance);
        } catch (Throwable ignored) {
            return null;
        }
    }

    public static Object getUdfTabFolder(Object udfDlg) {
        if (udfDlg == null || isDisposed(udfDlg)) return null;
        try {
            Field tfField = udfDlg.getClass().getDeclaredField("tabFolder");
            tfField.setAccessible(true);
            Object tf = tfField.get(udfDlg);
            if (tf != null && !isDisposed(tf)) return tf;
        } catch (Throwable ignored) {}
        return null;
    }

    public static int getCurrentOrLastUdfTabIndex() {
        if (!isFeatureEnabled("FormGenPreserveUdfTab")) return -1;
        try {
            Object udfDlg = getUdfDlg();
            Object tf = getUdfTabFolder(udfDlg);
            if (tf != null) {
                Method getSelIdx = tf.getClass().getMethod("getSelectionIndex");
                int idx = (Integer) getSelIdx.invoke(tf);
                if (idx >= 0) {
                    lastUdfTabIndex = idx;
                    return idx;
                }
            }
        } catch (Throwable ignored) {}
        return lastUdfTabIndex;
    }

    public static void onBeforeUdfLoadCode(Object udfDlg) {
        if (!isFeatureEnabled("FormGenPreserveUdfTab")) return;
        try {
            Object tf = getUdfTabFolder(udfDlg);
            if (tf != null) {
                Method getSelIdx = tf.getClass().getMethod("getSelectionIndex");
                int idx = (Integer) getSelIdx.invoke(tf);
                if (idx >= 0) {
                    lastUdfTabIndex = idx;
                }
            }
        } catch (Throwable ignored) {}
    }

    public static void onAfterUdfLoadCode(Object udfDlg) {
        if (!isFeatureEnabled("FormGenPreserveUdfTab")) return;
        if (lastUdfTabIndex >= 0) {
            restoreUdfTabSelection(udfDlg, lastUdfTabIndex);
        }
    }

    public static void restoreUdfTabSelection(Object udfDlg, int tabIndex) {
        if (!isFeatureEnabled("FormGenPreserveUdfTab")) return;
        if (udfDlg == null || tabIndex < 0 || isDisposed(udfDlg)) return;
        try {
            Object tf = getUdfTabFolder(udfDlg);
            if (tf == null) return;
            Field itemsField = udfDlg.getClass().getDeclaredField("tabItems");
            itemsField.setAccessible(true);
            Object[] tabItems = (Object[]) itemsField.get(udfDlg);
            if (tabItems != null && tabIndex < tabItems.length && tabItems[tabIndex] != null && !isDisposed(tabItems[tabIndex])) {
                Method setSel = tf.getClass().getMethod("setSelection", Class.forName("org.eclipse.swt.custom.CTabItem"));
                setSel.invoke(tf, tabItems[tabIndex]);
                lastUdfTabIndex = tabIndex;
            }
        } catch (Throwable t) {
            log("Error restoring UdfDlg tab selection: " + t.getMessage());
        }
    }

    public static void ensureUdfAndMainTabHooksInstalled(final Object display) {
        if (display == null || isDisposed(display)) return;
        try {
            discoverFormGen();
            if (formGenInstance == null) return;

            // 1. Hook UdfDlg.tabFolder Selection event to record user's selected tab (Init=0, Prompts=1, Post=2)
            final Object udfDlg = getUdfDlg();
            final Object udfTabFolder = getUdfTabFolder(udfDlg);
            if (udfTabFolder != null) {
                Method getData = udfTabFolder.getClass().getMethod("getData", String.class);
                if (!Boolean.TRUE.equals(getData.invoke(udfTabFolder, "SoleraUdfTabHook"))) {
                    Method setData = udfTabFolder.getClass().getMethod("setData", String.class, Object.class);
                    setData.invoke(udfTabFolder, "SoleraUdfTabHook", Boolean.TRUE);

                    Object listener = createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                        @Override
                        public void run() {
                            try {
                                int idx = (Integer) udfTabFolder.getClass().getMethod("getSelectionIndex").invoke(udfTabFolder);
                                if (idx >= 0) {
                                    lastUdfTabIndex = idx;
                                }
                            } catch (Throwable ignored) {}
                        }
                    });
                    Method addListener = udfTabFolder.getClass().getMethod("addListener", int.class, Class.forName("org.eclipse.swt.widgets.Listener"));
                    addListener.invoke(udfTabFolder, SWT_Selection, listener);
                }
            }

            // 2. Hook NewFormGen.mainTabFolder Selection event so switching open forms preserves UdfDlg's active tab
            Field mtfField = formGenInstance.getClass().getDeclaredField("mainTabFolder");
            mtfField.setAccessible(true);
            final Object mainTabFolder = mtfField.get(formGenInstance);
            if (mainTabFolder != null && !isDisposed(mainTabFolder)) {
                Method getData = mainTabFolder.getClass().getMethod("getData", String.class);
                if (!Boolean.TRUE.equals(getData.invoke(mainTabFolder, "SoleraMainTabHook"))) {
                    Method setData = mainTabFolder.getClass().getMethod("setData", String.class, Object.class);
                    setData.invoke(mainTabFolder, "SoleraMainTabHook", Boolean.TRUE);

                    Object listener = createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                        @Override
                        public void run() {
                            if (!isFeatureEnabled("FormGenPreserveUdfTab")) return;
                            final int targetTab = lastUdfTabIndex;
                            if (targetTab >= 0) {
                                restoreUdfTabSelection(getUdfDlg(), targetTab);
                                runOnDisplay(display, new Runnable() {
                                    @Override
                                    public void run() {
                                        restoreUdfTabSelection(getUdfDlg(), targetTab);
                                    }
                                });
                            }
                        }
                    });
                    Method addListener = mainTabFolder.getClass().getMethod("addListener", int.class, Class.forName("org.eclipse.swt.widgets.Listener"));
                    addListener.invoke(mainTabFolder, SWT_Selection, listener);
                }
            }
        } catch (Throwable ignored) {}
    }

    public static boolean onPreOpenFormgenFile(Object nfgInstance, File file) {
        if (!isFeatureEnabled("FormGenAlreadyOpenToast")) return false;
        try {
            if (nfgInstance != null && formGenInstance == null) {
                formGenInstance = nfgInstance;
                try {
                    Field shField = nfgInstance.getClass().getDeclaredField("shell");
                    shField.setAccessible(true);
                    mainShell = shField.get(nfgInstance);
                } catch (Throwable ignored) {}
            }
            return focusExistingTabIfOpen(file, true);
        } catch (Throwable t) {
            log("Error in onPreOpenFormgenFile: " + t.getMessage());
            return false;
        }
    }

    public static boolean focusExistingTabIfOpen(File file, boolean showToast) {
        if (file == null || formGenInstance == null) return false;
        if (showToast && !isFeatureEnabled("FormGenAlreadyOpenToast")) return false;
        try {
            Field f = formGenInstance.getClass().getDeclaredField("mainTabFolder");
            f.setAccessible(true);
            Object mainTabFolder = f.get(formGenInstance);
            if (mainTabFolder == null || isDisposed(mainTabFolder)) return false;

            Method getItemsMethod = mainTabFolder.getClass().getMethod("getItems");
            Object[] tabItems = (Object[]) getItemsMethod.invoke(mainTabFolder);
            if (tabItems == null) return false;

            String targetCanonical = null;
            try { targetCanonical = file.getCanonicalPath(); } catch (Throwable ignored) {}

            Object matchingTab = null;
            Object matchingViewer = null;

            for (Object tabItem : tabItems) {
                if (tabItem == null || isDisposed(tabItem)) continue;
                try {
                    Object viewer = tabItem.getClass().getMethod("getControl").invoke(tabItem);
                    if (viewer == null || isDisposed(viewer)) continue;

                    boolean inUse = false;
                    try {
                        Method fiuMethod = viewer.getClass().getMethod("fileIsInUse", File.class);
                        inUse = Boolean.TRUE.equals(fiuMethod.invoke(viewer, file));
                    } catch (Throwable ignored) {}

                    if (!inUse && targetCanonical != null) {
                        try {
                            File curFile = (File) viewer.getClass().getMethod("getCurrentFormgenFile").invoke(viewer);
                            if (curFile != null && curFile.getCanonicalPath().equalsIgnoreCase(targetCanonical)) {
                                inUse = true;
                            }
                        } catch (Throwable ignored) {}
                    }

                    if (inUse) {
                        matchingTab = tabItem;
                        matchingViewer = viewer;
                        break;
                    }
                } catch (Throwable ignored) {}
            }

            if (matchingTab == null || matchingViewer == null) return false;

            log("File is already open in FormGen: " + file.getAbsolutePath() + ". Switching focus without reloading.");
            int savedUdfTab = getCurrentOrLastUdfTabIndex();

            try {
                Method setSelectionMethod = mainTabFolder.getClass().getMethod("setSelection", Class.forName("org.eclipse.swt.custom.CTabItem"));
                setSelectionMethod.invoke(mainTabFolder, matchingTab);
            } catch (Throwable ignored) {}

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

            if (savedUdfTab >= 0) {
                restoreUdfTabSelection(getUdfDlg(), savedUdfTab);
            }

            try {
                Method m = formGenInstance.getClass().getMethod("returnFocusToForm");
                m.invoke(formGenInstance);
            } catch (Throwable ignored) {}

            activateMainShell();

            if (showToast && isFeatureEnabled("FormGenAlreadyOpenToast")) {
                showToastNotification("File already open, focus switched to the form you attempted to open.");
            }
            return true;
        } catch (Throwable t) {
            log("Error in focusExistingTabIfOpen: " + t.getMessage());
            return false;
        }
    }

    public static void showToastNotification(final String message) {
        if (message == null || message.isEmpty()) return;
        final Object display = getDisplay();
        if (display == null || isDisposed(display)) return;

        runOnDisplay(display, new Runnable() {
            @Override
            public void run() {
                try {
                    discoverFormGen();
                    if (mainShell == null || isDisposed(mainShell)) return;

                    Class<?> shellClass = Class.forName("org.eclipse.swt.widgets.Shell");
                    Class<?> stdImageClass = Class.forName("com.automate.controls.stdimages.StdImages$StdImage");
                    Class<?> nwClass = Class.forName("com.automate.controls.notifications.NotificationWindow");

                    Object iconInfo = null;
                    try {
                        Field iconField = stdImageClass.getField("IconInformation");
                        iconInfo = iconField.get(null);
                    } catch (Throwable ignored) {}

                    Constructor<?> cons = nwClass.getConstructor(shellClass, stdImageClass, String.class);
                    cons.newInstance(mainShell, iconInfo, message);
                    log("Displayed toast notification: " + message);
                } catch (Throwable t) {
                    log("Error displaying NotificationWindow toast: " + t.getMessage());
                }
            }
        });
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

                    // If the file is already open in FormGen, switch focus to its tab and show a toast without reloading
                    if (focusExistingTabIfOpen(file, true)) {
                        return;
                    }

                    int savedUdfTab = getCurrentOrLastUdfTabIndex();
                    log("Invoking openFormgenFile on: " + file.getAbsolutePath());
                    Method openMethod = formGenInstance.getClass().getDeclaredMethod("openFormgenFile", File.class, boolean.class);
                    openMethod.setAccessible(true);
                    Object result = openMethod.invoke(formGenInstance, file, false);
                    log("openFormgenFile returned: " + result);

                    if (savedUdfTab >= 0) {
                        restoreUdfTabSelection(getUdfDlg(), savedUdfTab);
                    }

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

                    int savedUdfTab = getCurrentOrLastUdfTabIndex();

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
                        int previousPage = 0;
                        try {
                            Object pg = matchingViewer.getClass().getMethod("getCurrentPageNumber").invoke(matchingViewer);
                            if (pg instanceof Integer) previousPage = Math.max(0, (Integer) pg);
                        } catch (Throwable ignored) {}
                        try {
                            Method openFormFileMethod = matchingViewer.getClass().getMethod("openFormFile", formOpenModeClass, File.class, Class.forName("com.automate.formgen.dialogs.checkinout.CheckoutInfo"));
                            openFormFileMethod.setAccessible(true);
                            Object res = openFormFileMethod.invoke(matchingViewer, normalMode, file, null);
                            log("Form reloaded in existing tab: " + res);
                        } catch (Throwable t) {
                            log("Error reloading form in viewer: " + t.getMessage());
                        }

                        // openFormFile resets currentPageNumber to -1 (via onClose/reset) and never re-shows a page.
                        // Without restoring it, any mouse click hits FormDef.getPage(-1) -> ArrayIndexOutOfBounds.
                        try {
                            int numPages = 1;
                            try {
                                Object np = matchingViewer.getClass().getMethod("getNumPages").invoke(matchingViewer);
                                if (np instanceof Integer) numPages = (Integer) np;
                            } catch (Throwable ignored) {}
                            int pageToShow = (previousPage >= 0 && previousPage < numPages) ? previousPage : 0;
                            try {
                                Method spc = formGenInstance.getClass().getDeclaredMethod("setupPageControl", int.class);
                                spc.setAccessible(true);
                                spc.invoke(formGenInstance, pageToShow);
                            } catch (Throwable t) {
                                Method sp = matchingViewer.getClass().getMethod("showPage", int.class);
                                sp.invoke(matchingViewer, pageToShow);
                            }
                        } catch (Throwable t) {
                            log("Error restoring page after reload: " + t.getMessage());
                        }

                        // Re-bind FormGen's dialogs (code/prompts, field properties), toolbar and page control to the
                        // freshly loaded form. The earlier onTabSelected ran against the pre-reload form definition,
                        // which left them showing stale data until the next push re-selected the tab.
                        try {
                            Method onTabSel2 = formGenInstance.getClass().getDeclaredMethod("onTabSelected", Class.forName("org.eclipse.swt.custom.CTabItem"));
                            onTabSel2.setAccessible(true);
                            onTabSel2.invoke(formGenInstance, matchingTab);
                        } catch (Throwable t) {
                            log("Error re-selecting tab after reload: " + t.getMessage());
                        }

                        if (savedUdfTab >= 0) {
                            restoreUdfTabSelection(getUdfDlg(), savedUdfTab);
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

                        refreshOpenFormDialogs(display, matchingViewer, file, savedUdfTab);

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

                            if (savedUdfTab >= 0) {
                                restoreUdfTabSelection(getUdfDlg(), savedUdfTab);
                            }

                            try {
                                Method m = formGenInstance.getClass().getMethod("setCurrentTabTitle");
                                m.invoke(formGenInstance);
                            } catch (Throwable ignored) {}
                            try {
                                Method m = formGenInstance.getClass().getDeclaredMethod("setCurrentFormgenFilenameTitle");
                                m.setAccessible(true);
                                m.invoke(formGenInstance);
                            } catch (Throwable ignored) {}

                            refreshOpenFormDialogs(display, newViewer, file, savedUdfTab);

                            try {
                                Method m = formGenInstance.getClass().getMethod("returnFocusToForm");
                                m.invoke(formGenInstance);
                            } catch (Throwable ignored) {}
                        } catch (Throwable t) {
                            log("Direct createNewTab failed, falling back to openFormgenFile: " + t.getMessage());
                            Method openMethod = formGenInstance.getClass().getDeclaredMethod("openFormgenFile", File.class, boolean.class);
                            openMethod.setAccessible(true);
                            openMethod.invoke(formGenInstance, file, false);
                            if (savedUdfTab >= 0) {
                                restoreUdfTabSelection(getUdfDlg(), savedUdfTab);
                            }
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

    private static void refreshOpenFormDialogs(Object display, Object viewer, File file, int savedUdfTab) {
        if (display == null || isDisposed(display) || viewer == null) return;
        try {
            Method getFormDefMethod = viewer.getClass().getMethod("getFormDef");
            Object newFormDef = getFormDefMethod.invoke(viewer);
            if (newFormDef == null) return;

            // 1. Explicitly refresh UdfDlg (dlgCode) if visible, preserving its active tab (Init / Prompts / Post)
            if (formGenInstance != null) {
                try {
                    Object dlgCode = getUdfDlg();
                    if (dlgCode != null && !isDisposed(dlgCode)) {
                        Method isVisMethod = dlgCode.getClass().getMethod("isVisible");
                        if (Boolean.TRUE.equals(isVisMethod.invoke(dlgCode))) {
                            int tabToRestore = savedUdfTab >= 0 ? savedUdfTab : getCurrentOrLastUdfTabIndex();
                            boolean editAllowed = true;
                            try {
                                Method eaMethod = formGenInstance.getClass().getDeclaredMethod("editAllowed");
                                eaMethod.setAccessible(true);
                                editAllowed = Boolean.TRUE.equals(eaMethod.invoke(formGenInstance));
                            } catch (Throwable ignored) {}
                            Method loadCodeMethod = dlgCode.getClass().getMethod("loadCode", boolean.class, newFormDef.getClass());
                            loadCodeMethod.invoke(dlgCode, editAllowed, newFormDef);
                            if (tabToRestore >= 0) {
                                restoreUdfTabSelection(dlgCode, tabToRestore);
                            }
                            log("Refreshed open UdfDlg with updated FormDef (preserved tab index=" + tabToRestore + ").");
                        }
                    }
                } catch (Throwable t) {
                    log("Error refreshing UdfDlg: " + t.getMessage());
                }
            }

            // 2. Refresh any open FormPropertiesDlg shells
            Method getShellsMethod = display.getClass().getMethod("getShells");
            Object[] shells = (Object[]) getShellsMethod.invoke(display);
            if (shells == null) return;

            for (Object s : shells) {
                if (s == null || isDisposed(s)) continue;
                try {
                    Boolean vis = (Boolean) s.getClass().getMethod("isVisible").invoke(s);
                    if (!Boolean.TRUE.equals(vis)) continue;

                    String title = (String) s.getClass().getMethod("getText").invoke(s);
                    if (title != null && title.contains("Form Properties")) {
                        Object formPropsDlg = null;
                        try {
                            formPropsDlg = s.getClass().getMethod("getData", String.class).invoke(s, "SoleraFormPropsDlg");
                        } catch (Throwable ignored) {}
                        if (formPropsDlg == null) {
                            formPropsDlg = extractTargetObject(s, "com.automate.formgen.dialogs.FormPropertiesDlg", 0);
                        }
                        if (formPropsDlg != null) {
                            Field fdField = formPropsDlg.getClass().getDeclaredField("formdef");
                            fdField.setAccessible(true);
                            fdField.set(formPropsDlg, newFormDef);

                            // Clear listStates and listOEMs before calling loadData to prevent duplicate list items
                            try {
                                Field lsField = formPropsDlg.getClass().getDeclaredField("listStates");
                                lsField.setAccessible(true);
                                Object listStates = lsField.get(formPropsDlg);
                                if (listStates != null) {
                                    listStates.getClass().getMethod("removeAll").invoke(listStates);
                                }
                            } catch (Throwable ignored) {}

                            try {
                                Field loField = formPropsDlg.getClass().getDeclaredField("listOEMs");
                                loField.setAccessible(true);
                                Object listOEMs = loField.get(formPropsDlg);
                                if (listOEMs != null) {
                                    listOEMs.getClass().getMethod("removeAll").invoke(listOEMs);
                                }
                            } catch (Throwable ignored) {}

                            String fileName = file != null ? file.getName() : "";
                            try {
                                Field fnField = formPropsDlg.getClass().getDeclaredField("lblFileName");
                                fnField.setAccessible(true);
                                Object lblFn = fnField.get(formPropsDlg);
                                if (lblFn != null && !isDisposed(lblFn) && (fileName == null || fileName.isEmpty())) {
                                    fileName = (String) lblFn.getClass().getMethod("getText").invoke(lblFn);
                                }
                            } catch (Throwable ignored) {}

                            Method loadDataMethod = formPropsDlg.getClass().getDeclaredMethod("loadData", String.class);
                            loadDataMethod.setAccessible(true);
                            loadDataMethod.invoke(formPropsDlg, fileName != null ? fileName : "");
                            log("Refreshed open FormPropertiesDlg with updated FormDef.");
                        }
                    }
                } catch (Throwable dlgErr) {
                    log("Error refreshing FormPropertiesDlg shell: " + dlgErr.getMessage());
                }
            }
        } catch (Throwable t) {
            log("Error in refreshOpenFormDialogs: " + t.getMessage());
        }
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

    private static void writePortFile(int port) throws IOException {
        FileWriter fw = new FileWriter(PORT_FILE);
        try {
            fw.write(String.valueOf(port));
        } finally {
            fw.close();
        }
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

                    final int listenPort = port;
                    writePortFile(listenPort);

                    Thread watchdog = new Thread(new Runnable() {
                        @Override
                        public void run() {
                            while (true) {
                                try {
                                    Thread.sleep(2000);
                                    File pf = new File(PORT_FILE);
                                    if (!pf.exists()) {
                                        writePortFile(listenPort);
                                        log("Port file was missing; recreated for port " + listenPort);
                                    }
                                } catch (InterruptedException ie) {
                                    return;
                                } catch (Throwable ignored) {}
                            }
                        }
                    }, "FormGenAgent-PortWatchdog");
                    watchdog.setDaemon(true);
                    watchdog.start();

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
                    final Object disp = getDisplay();
                    if (disp != null && !isDisposed(disp)) {
                        runOnDisplay(disp, new Runnable() {
                            @Override
                            public void run() {
                                reconcileFeaturesOnUiThread(disp);
                            }
                        });
                    }
                    OutputStream out = socket.getOutputStream();
                    out.write("OK\n".getBytes("UTF-8"));
                    out.flush();
                } else if (line.equalsIgnoreCase("GET_ACTIVE_FORM")) {
                    String formPath = getActiveFormFilePath();
                    OutputStream out = socket.getOutputStream();
                    out.write(((formPath != null ? formPath : "") + "\n").getBytes("UTF-8"));
                    out.flush();
                } else if (line.equalsIgnoreCase("GET_OPEN_FORMS")) {
                    List<String> openForms = getOpenFormFilePaths();
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < openForms.size(); i++) {
                        if (i > 0) sb.append("|");
                        sb.append(openForms.get(i));
                    }
                    OutputStream out = socket.getOutputStream();
                    out.write((sb.toString() + "\n").getBytes("UTF-8"));
                    out.flush();
                } else if (line.equalsIgnoreCase("SHUTDOWN")) {
                    try {
                        if (isFeatureEnabled("FormGenAutoRestoreForms")) {
                            List<String> openForms = getOpenFormFilePaths();
                            if (!openForms.isEmpty()) {
                                FileWriter fw = new FileWriter(SESSION_FORMS_FILE, false);
                                try {
                                    for (String p : openForms) fw.write(p + "\n");
                                } finally {
                                    fw.close();
                                }
                            }
                        } else {
                            File sf = new File(SESSION_FORMS_FILE);
                            if (sf.exists()) sf.delete();
                        }
                    } catch (Throwable ignored) {}
                    try { new File(PORT_FILE).delete(); } catch (Throwable ignored) {}
                    OutputStream out = socket.getOutputStream();
                    out.write("OK\n".getBytes("UTF-8"));
                    out.flush();
                    new Thread(new Runnable() {
                        @Override
                        public void run() {
                            try { Thread.sleep(150); } catch (Throwable ignored) {}
                            System.exit(0);
                        }
                    }, "FormGenAgent-Shutdown").start();
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
        final Object display = getDisplay();
        if (display != null && !isDisposed(display)) {
            runOnDisplay(display, new Runnable() {
                @Override
                public void run() {
                    try {
                        mainShell.getClass().getMethod("setMinimized", boolean.class).invoke(mainShell, false);
                        mainShell.getClass().getMethod("setActive").invoke(mainShell);
                        mainShell.getClass().getMethod("forceActive").invoke(mainShell);

                        // If any secondary dialog shell (e.g. Form Properties, UDF/Prompts, Form Search, Publish)
                        // is currently open and visible, keep it above mainShell so it isn't sent to the background.
                        Method getShellsMethod = display.getClass().getMethod("getShells");
                        Object[] shells = (Object[]) getShellsMethod.invoke(display);
                        if (shells != null) {
                            for (Object s : shells) {
                                if (s != null && s != mainShell && !isDisposed(s)) {
                                    Boolean vis = (Boolean) s.getClass().getMethod("isVisible").invoke(s);
                                    if (Boolean.TRUE.equals(vis)) {
                                        s.getClass().getMethod("setActive").invoke(s);
                                        s.getClass().getMethod("forceActive").invoke(s);
                                    }
                                }
                            }
                        }
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
        if (!isFeatureEnabled("FormGenEditorUndoRedo")) return;
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
            addFilterMethod.invoke(display, SWT_Selection, listenerProxy);   // SWT.Selection = 13
            addFilterMethod.invoke(display, SWT_FocusIn, listenerProxy);     // SWT.FocusIn = 15
            addFilterMethod.invoke(display, SWT_Show, listenerProxy);        // SWT.Show = 22
            addFilterMethod.invoke(display, SWT_Activate, listenerProxy);    // SWT.Activate = 26
            addFilterMethod.invoke(display, SWT_MenuDetect, listenerProxy);  // SWT.MenuDetect = 35

            log("SWT context menu & dialog filters installed (KeyDown=1, Selection=13, FocusIn=15, Show=22, Activate=26, MenuDetect=35).");
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
                    reconcileFeaturesOnUiThread(display);
                } catch (Throwable ignored) {}
                if (!isDisposed(display)) {
                    runTimer(display, 500, this);
                }
            }
        });
    }

    public static void reconcileFeaturesOnUiThread(final Object display) {
        if (display == null || isDisposed(display)) return;
        try {
            ensureUdfAndMainTabHooksInstalled(display);
            saveOpenFormsSnapshotOnUiThread();
            Method getShellsMethod = display.getClass().getMethod("getShells");
            Object[] shells = (Object[]) getShellsMethod.invoke(display);
            boolean autoFillEnabled = isFeatureEnabled("FormGenDialogAutoFill");
            boolean undoEnabled = isFeatureEnabled("FormGenEditorUndoRedo");
            boolean contextMenusEnabled = isFeatureEnabled("FormGenContextMenus");
            if (shells != null) {
                for (Object s : shells) {
                    if (s != null && !isDisposed(s)) {
                        if (autoFillEnabled) {
                            enhanceDialogShell(display, s);
                        } else {
                            removeEnhanceDialogShell(s);
                        }
                        if (undoEnabled) {
                            attachUndoManagersRecursively(s);
                        }
                        if (!contextMenusEnabled) {
                            removeCustomMenusRecursively(s);
                        }
                    }
                }
            }
        } catch (Throwable ignored) {}
    }

    private static void removeCustomMenusRecursively(Object widget) {
        if (widget == null || isDisposed(widget)) return;
        try {
            Method getDataMethod = widget.getClass().getMethod("getData", String.class);
            Method setDataMethod = widget.getClass().getMethod("setData", String.class, Object.class);
            Object customMenu = getDataMethod.invoke(widget, "SoleraCustomMenu");
            if (customMenu != null) {
                try {
                    Class<?> menuClass = Class.forName("org.eclipse.swt.widgets.Menu");
                    Method getMenuMethod = widget.getClass().getMethod("getMenu");
                    Object curMenu = getMenuMethod.invoke(widget);
                    if (curMenu == customMenu) {
                        Method setMenuMethod = widget.getClass().getMethod("setMenu", menuClass);
                        setMenuMethod.invoke(widget, new Object[] { null });
                    }
                } catch (Throwable ignored) {}
                try {
                    if (!isDisposed(customMenu)) {
                        customMenu.getClass().getMethod("dispose").invoke(customMenu);
                    }
                } catch (Throwable ignored) {}
                setDataMethod.invoke(widget, "SoleraCustomMenu", null);
            }

            Class<?> compositeClass = Class.forName("org.eclipse.swt.widgets.Composite");
            if (compositeClass.isInstance(widget)) {
                Method getChildrenMethod = compositeClass.getMethod("getChildren");
                Object[] children = (Object[]) getChildrenMethod.invoke(widget);
                if (children != null) {
                    for (Object c : children) {
                        removeCustomMenusRecursively(c);
                    }
                }
            }
        } catch (Throwable ignored) {}
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

            if (eventType == SWT_Selection) {
                try {
                    Object udfDlg = getUdfDlg();
                    if (udfDlg != null) {
                        Object udfTabFolder = getUdfTabFolder(udfDlg);
                        if (udfTabFolder != null && widget == udfTabFolder) {
                            Method getSelIdx = udfTabFolder.getClass().getMethod("getSelectionIndex");
                            int idx = (Integer) getSelIdx.invoke(udfTabFolder);
                            if (idx >= 0) {
                                lastUdfTabIndex = idx;
                            }
                        }
                    }
                } catch (Throwable ignored) {}
                return;
            } else if (eventType == SWT_KeyDown) {
                if (widget != null && isFeatureEnabled("FormGenEditorUndoRedo")) {
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
                if (widget != null && isFeatureEnabled("FormGenEditorUndoRedo")) {
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
                            if (Boolean.TRUE.equals(getDataMethod.invoke(widget, "SoleraOwnedMenu"))) {
                                return;
                            }
                        } catch (Throwable ignored) {}
                        // Always call injectSoleraMenuItems on native pop-up menus so disabled features are stripped immediately
                        injectSoleraMenuItems(display, widget);
                    }
                } else if (shellClass.isInstance(widget)) {
                    if (isFeatureEnabled("FormGenDialogAutoFill")) {
                        enhanceDialogShell(display, widget);
                    } else {
                        removeEnhanceDialogShell(widget);
                    }
                    attachUndoManagersRecursively(widget);
                }
            } else if (eventType == SWT_Activate) {
                if (shellClass.isInstance(widget)) {
                    if (isFeatureEnabled("FormGenDialogAutoFill")) {
                        enhanceDialogShell(display, widget);
                    } else {
                        removeEnhanceDialogShell(widget);
                    }
                    attachUndoManagersRecursively(widget);
                }
            } else if (eventType == SWT_MenuDetect) {
                if (controlClass.isInstance(widget)) {
                    String clsName = widget.getClass().getName();
                    if (clsName.contains("PdfViewer")) {
                        try {
                            lastMenuDetectX = eventObj.getClass().getField("x").getInt(eventObj);
                            lastMenuDetectY = eventObj.getClass().getField("y").getInt(eventObj);
                        } catch (Throwable ignored) {}
                        return;
                    }
                    if (!isFeatureEnabled("FormGenContextMenus")) {
                        removeCustomMenusRecursively(widget);
                        return;
                    }
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
                        boolean isOurMenu = false;
                        if (existingMenu != null && !isDisposed(existingMenu)) {
                            try {
                                isOurMenu = Boolean.TRUE.equals(existingMenu.getClass().getMethod("getData", String.class).invoke(existingMenu, "SoleraOwnedMenu"));
                            } catch (Throwable ignored) {}
                        }
                        if (existingMenu == null || isDisposed(existingMenu) || isOurMenu) {
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
        if (!isFeatureEnabled("FormGenContextMenus")) {
            removeCustomMenusRecursively(control);
            return;
        }
        try {
            removeCustomMenusRecursively(control);
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

            // 0. Undo, Redo (if FormGenEditorUndoRedo is enabled)
            if (isFeatureEnabled("FormGenEditorUndoRedo")) {
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
            }

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
                    refreshLiveVariablesSync();
                    runOnDisplay(display, new Runnable() {
                        @Override
                        public void run() {
                            String codeToPaste = liveVariables.get("ActiveCodeSnippetText");
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
            setDataMethod.invoke(menu, "SoleraOwnedMenu", Boolean.TRUE);
            control.getClass().getMethod("setData", String.class, Object.class).invoke(control, "SoleraCustomMenu", menu);

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
        if (!isFeatureEnabled("FormGenContextMenus")) {
            removeCustomMenusRecursively(control);
            return;
        }
        try {
            removeCustomMenusRecursively(control);
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
            setDataMethod.invoke(newMenu, "SoleraOwnedMenu", Boolean.TRUE);
            control.getClass().getMethod("setData", String.class, Object.class).invoke(control, "SoleraCustomMenu", newMenu);

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
            boolean contextMenusEnabled = isFeatureEnabled("FormGenContextMenus");
            boolean canvasPasteEnabled = isFeatureEnabled("FormGenCanvasPasteField");

            // Always remove previous Solera items first so disabled features are immediately stripped from native menus
            Method getItemsMethod = menu.getClass().getMethod("getItems");
            Object[] items = (Object[]) getItemsMethod.invoke(menu);
            boolean isEmptyCanvasMenu = false;
            if (items != null) {
                for (int i = items.length - 1; i >= 0; i--) {
                    Object it = items[i];
                    String itText = "";
                    try { itText = (String) it.getClass().getMethod("getText").invoke(it); } catch (Throwable ignored) {}
                    if (itText != null && (itText.contains("Drop text field here") || itText.contains("Drop specific field here"))) {
                        isEmptyCanvasMenu = true;
                    }
                    Object tag = null;
                    try { tag = it.getClass().getMethod("getData", String.class).invoke(it, "SoleraItem"); } catch (Throwable ignored) {}
                    if (Boolean.TRUE.equals(tag) || (itText != null && (itText.contains("Solera") || itText.contains("Parse Field") || itText.contains("Paste Code Block") || itText.contains("FormGen Utilities")))) {
                        it.getClass().getMethod("dispose").invoke(it);
                    }
                }
            }

            if (!contextMenusEnabled && !canvasPasteEnabled) return;

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
            if (fieldFormula != null && !fieldFormula.trim().isEmpty() && contextMenusEnabled) {
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
            } else if (isEmptyCanvasMenu && !hasSelectedCanvasFields() && canvasPasteEnabled) {
                final int[] dropPt = extractDropPointFromMenu(menu, display);
                String activeCode = null;
                String activeType = null;
                synchronized (liveVariables) {
                    activeCode = liveVariables.get("ActiveCodeSnippetText");
                    activeType = liveVariables.get("ActiveCodeSnippetOutputType");
                }
                if (activeCode == null || activeCode.trim().isEmpty()) {
                    activeCode = getClipboardText();
                    activeType = inferExpressionOutputType(activeCode);
                } else if (activeType == null || activeType.trim().isEmpty()) {
                    activeType = inferExpressionOutputType(activeCode);
                }

                String typeLabel = "NUMERIC".equalsIgnoreCase(activeType) ? "Numeric" : "Text";
                Object itemDropCode = itemCons.newInstance(menu, SWT_PUSH);
                itemDropCode.getClass().getMethod("setText", String.class).invoke(itemDropCode, "📋 Paste Code Block as New " + typeLabel + " Field");
                setDataMethod.invoke(itemDropCode, "SoleraItem", Boolean.TRUE);

                Object listenerDropCode = createListenerProxy(display.getClass().getClassLoader(), new Runnable() {
                    @Override
                    public void run() {
                        dropCodeBlockFieldAtPT(display, dropPt[0], dropPt[1]);
                    }
                });
                addListenerMethod.invoke(itemDropCode, SWT_Selection, listenerDropCode);
            }

            if (contextMenusEnabled) {
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
            }

            // Mark the menu as having Solera injected
            Method setMenuData = menu.getClass().getMethod("setData", String.class, Object.class);
            setMenuData.invoke(menu, "SoleraInjected", Boolean.TRUE);
        } catch (Throwable t) {
            log("Error injecting Solera menu items: " + t.getMessage());
        }
    }

    private static boolean hasSelectedCanvasFields() {
        if (formGenInstance == null) return false;
        try {
            Field fvField = formGenInstance.getClass().getDeclaredField("formViewer");
            fvField.setAccessible(true);
            Object formViewer = fvField.get(formGenInstance);
            if (formViewer == null) return false;

            Field sfField = formViewer.getClass().getDeclaredField("selectedFields");
            sfField.setAccessible(true);
            Object selectedFields = sfField.get(formViewer);
            if (selectedFields == null) return false;

            Method sizeMethod = selectedFields.getClass().getMethod("size");
            int size = (Integer) sizeMethod.invoke(selectedFields);
            return size > 0;
        } catch (Throwable ignored) {}
        return false;
    }

    private static int[] extractDropPointFromMenu(Object menu, Object display) {
        // 1. Try extracting val$ptPT from "Drop text field here" MenuItem selection listener
        try {
            Method getItemsMethod = menu.getClass().getMethod("getItems");
            Object[] items = (Object[]) getItemsMethod.invoke(menu);
            if (items != null) {
                for (Object it : items) {
                    if (it == null || isDisposed(it)) continue;
                    String txt = (String) it.getClass().getMethod("getText").invoke(it);
                    if (txt != null && (txt.contains("Drop text field here") || txt.contains("Drop numeric field here"))) {
                        Method getListenersMethod = it.getClass().getMethod("getListeners", int.class);
                        Object[] listeners = (Object[]) getListenersMethod.invoke(it, SWT_Selection);
                        if (listeners != null) {
                            for (Object l : listeners) {
                                Object targetListener = l;
                                try {
                                    Field elField = l.getClass().getDeclaredField("eventListener");
                                    elField.setAccessible(true);
                                    Object unwrapped = elField.get(l);
                                    if (unwrapped != null) targetListener = unwrapped;
                                } catch (Throwable ignored) {}

                                for (Field f : targetListener.getClass().getDeclaredFields()) {
                                    f.setAccessible(true);
                                    Object val = f.get(targetListener);
                                    if (val != null && "org.eclipse.swt.graphics.Point".equals(val.getClass().getName())) {
                                        int px = val.getClass().getField("x").getInt(val);
                                        int py = val.getClass().getField("y").getInt(val);
                                        return new int[] { px, py };
                                    }
                                }
                            }
                        }
                    }
                }
            }
        } catch (Throwable ignored) {}

        // 2. Fallback: convert lastMenuDetectX/Y (or current cursor location) via PdfViewer.windowPixelToFormPT
        try {
            if (formGenInstance != null && display != null) {
                Field fvField = formGenInstance.getClass().getDeclaredField("formViewer");
                fvField.setAccessible(true);
                Object formViewer = fvField.get(formGenInstance);
                if (formViewer != null) {
                    int screenX = lastMenuDetectX;
                    int screenY = lastMenuDetectY;
                    if (screenX < 0 || screenY < 0) {
                        Object cursorPt = display.getClass().getMethod("getCursorLocation").invoke(display);
                        screenX = cursorPt.getClass().getField("x").getInt(cursorPt);
                        screenY = cursorPt.getClass().getField("y").getInt(cursorPt);
                    }
                    Class<?> controlClass = Class.forName("org.eclipse.swt.widgets.Control");
                    Class<?> pointClass = Class.forName("org.eclipse.swt.graphics.Point");
                    Object mappedPt = display.getClass().getMethod("map", controlClass, controlClass, int.class, int.class)
                        .invoke(display, null, formViewer, screenX, screenY);
                    Method toPtMethod = formViewer.getClass().getDeclaredMethod("windowPixelToFormPT", pointClass);
                    toPtMethod.setAccessible(true);
                    Object formPt = toPtMethod.invoke(formViewer, mappedPt);
                    if (formPt != null) {
                        int px = formPt.getClass().getField("x").getInt(formPt);
                        int py = formPt.getClass().getField("y").getInt(formPt);
                        return new int[] { px, py };
                    }
                }
            }
        } catch (Throwable ignored) {}

        return new int[] { 720, 720 };
    }

    private static void dropCodeBlockFieldAtPT(final Object display, final int ptX, final int ptY) {
        new Thread(new Runnable() {
            @Override
            public void run() {
                refreshLiveVariablesSync();
                runOnDisplay(display, new Runnable() {
                    @Override
                    public void run() {
                        try {
                            if (formGenInstance == null) return;
                            Field fvField = formGenInstance.getClass().getDeclaredField("formViewer");
                            fvField.setAccessible(true);
                            Object formViewer = fvField.get(formGenInstance);
                            if (formViewer == null) return;

                            String codeToPaste = null;
                            String outputType = null;
                            synchronized (liveVariables) {
                                codeToPaste = liveVariables.get("ActiveCodeSnippetText");
                                outputType = liveVariables.get("ActiveCodeSnippetOutputType");
                            }
                            if (codeToPaste == null || codeToPaste.trim().isEmpty()) {
                                codeToPaste = getClipboardText();
                                outputType = inferExpressionOutputType(codeToPaste);
                            } else if (outputType == null || outputType.trim().isEmpty()) {
                                outputType = inferExpressionOutputType(codeToPaste);
                            }

                            if (codeToPaste == null) {
                                codeToPaste = "";
                            }
                            String expr = codeToPaste.trim();

                            ClassLoader cl = formGenInstance.getClass().getClassLoader();
                            Class<?> formatClass = Class.forName("com.automate.formgen.formfields.FieldDefaultFormat", true, cl);
                            Object format = null;

                            // If expr is a single deal field F<num>, check FormDatabaseCache first
                            if (expr.matches("^[Ff]\\d+$")) {
                                try {
                                    int fieldNum = Integer.parseInt(expr.substring(1));
                                    Class<?> cacheClass = Class.forName("com.automate.formgen.cache.FormDatabaseCache", true, cl);
                                    Object cacheInst = cacheClass.getMethod("getInstance").invoke(null);
                                    Object descriptor = cacheClass.getMethod("getDealFieldDescriptor", int.class).invoke(cacheInst, fieldNum);
                                    if (descriptor != null) {
                                        format = descriptor.getClass().getMethod("getDefaultFormat").invoke(descriptor);
                                    }
                                } catch (Throwable ignored) {}
                            }

                            if (format == null) {
                                boolean isNumeric = "NUMERIC".equalsIgnoreCase(outputType)
                                    || "NUMERIC".equalsIgnoreCase(inferExpressionOutputType(expr));
                                if (isNumeric) {
                                    Field stdNumField = formatClass.getField("standardNumeric");
                                    format = stdNumField.get(null);
                                } else {
                                    Constructor<?> fmtCons = formatClass.getConstructor(int.class);
                                    format = fmtCons.newInstance(30);
                                }
                            }

                            Method dropWorker = formViewer.getClass().getMethod(
                                "dropExprAtPTLocationWorker",
                                String.class,
                                formatClass,
                                int.class,
                                int.class,
                                boolean.class
                            );
                            Object createdField = dropWorker.invoke(formViewer, expr, format, ptX, ptY, false);
                            log("Dropped code block field at (" + ptX + ", " + ptY + ") as "
                                + ("NUMERIC".equalsIgnoreCase(outputType) ? "NUMERIC" : "TEXT")
                                + " with expr: " + expr + " -> " + createdField);
                        } catch (Throwable t) {
                            log("Error in dropCodeBlockFieldAtPT: " + t.getMessage());
                        }
                    }
                });
            }
        }, "FormGenAgent-DropCodeBlock").start();
    }

    public static String inferExpressionOutputType(String rawExpr) {
        if (rawExpr == null) return "TEXT";
        String s = rawExpr.trim();
        if (s.isEmpty()) return "TEXT";

        while (s.startsWith("(") && s.endsWith(")") && isWrappedByOuterParens(s)) {
            s = s.substring(1, s.length() - 1).trim();
        }
        if (s.isEmpty()) return "TEXT";

        if ((s.startsWith("\"") && s.endsWith("\"")) || (s.startsWith("'") && s.endsWith("'"))) {
            return "TEXT";
        }
        if (s.matches("^[+-]?(\\d+(\\.\\d+)?|\\.\\d+)$")) {
            return "NUMERIC";
        }

        // Top-level binary operator scan outside parens/quotes
        boolean hasTopLevelPlus = false;
        boolean hasTopLevelArith = false;
        List<String> topLevelPlusOperands = new ArrayList<String>();
        int depth = 0;
        boolean inQuotes = false;
        char quoteChar = 0;
        int lastSplit = 0;

        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (inQuotes) {
                if (c == quoteChar) inQuotes = false;
                continue;
            }
            if (c == '"' || c == '\'') {
                inQuotes = true;
                quoteChar = c;
                continue;
            }
            if (c == '(') { depth++; continue; }
            if (c == ')') { if (depth > 0) depth--; continue; }

            if (depth == 0) {
                if (c == '+') {
                    String prev = s.substring(lastSplit, i).trim();
                    if (!prev.isEmpty()) {
                        hasTopLevelPlus = true;
                        topLevelPlusOperands.add(prev);
                        lastSplit = i + 1;
                    }
                } else if (c == '*' || c == '/' || c == '%') {
                    hasTopLevelArith = true;
                } else if (c == '-') {
                    String prev = s.substring(lastSplit, i).trim();
                    if (!prev.isEmpty()) {
                        hasTopLevelArith = true;
                    }
                }
            }
        }
        if (hasTopLevelPlus) {
            topLevelPlusOperands.add(s.substring(lastSplit).trim());
            boolean allNumeric = true;
            for (String op : topLevelPlusOperands) {
                if (!"NUMERIC".equals(inferExpressionOutputType(op))) {
                    allNumeric = false;
                    break;
                }
            }
            return allNumeric ? "NUMERIC" : "TEXT";
        }
        if (hasTopLevelArith) {
            return "NUMERIC";
        }

        // Function call Name(args...)
        int openParen = s.indexOf('(');
        if (openParen > 0 && s.endsWith(")")) {
            String candidatePrefix = s.substring(0, openParen).trim();
            String argsBody = s.substring(openParen);
            if (isValidIdentifier(candidatePrefix) && isWrappedByOuterParens(argsBody)) {
                String funcName = candidatePrefix.toUpperCase(Locale.ROOT);
                String innerArgs = argsBody.substring(1, argsBody.length() - 1);
                List<String> args = splitTopLevelArgs(innerArgs);

                if ("NUM".equals(funcName) || "ABS".equals(funcName) || "ROUND".equals(funcName)
                    || "TRUNC".equals(funcName) || "FLOOR".equals(funcName) || "CEILING".equals(funcName)
                    || "CEIL".equals(funcName) || "MIN".equals(funcName) || "MAX".equals(funcName)
                    || "SUM".equals(funcName) || "AVG".equals(funcName) || "LEN".equals(funcName)
                    || "LENGTH".equals(funcName) || "FIND".equals(funcName) || "FINDR".equals(funcName)
                    || "INDEXOF".equals(funcName) || "DAYS".equals(funcName) || "MONTHS".equals(funcName)
                    || "YEARS".equals(funcName) || "DAY".equals(funcName) || "MONTH".equals(funcName)
                    || "YEAR".equals(funcName) || "MOD".equals(funcName) || "POW".equals(funcName)
                    || "SQRT".equals(funcName) || "INT".equals(funcName) || "VAL".equals(funcName)
                    || "TONUM".equals(funcName) || "TONUMBER".equals(funcName)) {
                    return "NUMERIC";
                }
                if ("TEXT".equals(funcName) || "STR".equals(funcName) || "STRING".equals(funcName)
                    || "SUB".equals(funcName) || "SUBSTR".equals(funcName) || "SUBSTRING".equals(funcName)
                    || "LEFT".equals(funcName) || "RIGHT".equals(funcName) || "MID".equals(funcName)
                    || "TRIM".equals(funcName) || "LTRIM".equals(funcName) || "RTRIM".equals(funcName)
                    || "UPPER".equals(funcName) || "LOWER".equals(funcName) || "PROPER".equals(funcName)
                    || "PADL".equals(funcName) || "PADR".equals(funcName) || "LPAD".equals(funcName)
                    || "RPAD".equals(funcName) || "REPLACE".equals(funcName) || "FORMAT".equals(funcName)
                    || "DATE".equals(funcName) || "TODAY".equals(funcName) || "NOW".equals(funcName)
                    || "CHR".equals(funcName) || "CONCAT".equals(funcName) || "TOTEXT".equals(funcName)) {
                    return "TEXT";
                }
                if ("IF".equals(funcName) || "IIF".equals(funcName)) {
                    String trueType = args.size() >= 2 ? inferExpressionOutputType(args.get(1)) : "TEXT";
                    String falseType = args.size() >= 3 ? inferExpressionOutputType(args.get(2)) : trueType;
                    return ("NUMERIC".equals(trueType) && "NUMERIC".equals(falseType)) ? "NUMERIC" : "TEXT";
                }
                if ("CASE".equals(funcName) || "CASELTE".equals(funcName) || "MATCHRANGE".equals(funcName) || "SWITCH".equals(funcName)) {
                    if (args.size() < 3) return "TEXT";
                    List<String> resultExprs = new ArrayList<String>();
                    for (int i = 2; i < args.size(); i += 2) {
                        resultExprs.add(args.get(i));
                    }
                    if (args.size() % 2 == 0) {
                        resultExprs.add(args.get(args.size() - 1));
                    }
                    if (resultExprs.isEmpty()) return "TEXT";
                    for (String r : resultExprs) {
                        if (!"NUMERIC".equals(inferExpressionOutputType(r))) return "TEXT";
                    }
                    return "NUMERIC";
                }
                return "TEXT";
            }
        }

        // Single deal field F<num> via FormDatabaseCache if available
        if (s.matches("^[Ff]\\d+$") && formGenInstance != null) {
            try {
                int fieldNum = Integer.parseInt(s.substring(1));
                ClassLoader cl = formGenInstance.getClass().getClassLoader();
                Class<?> cacheClass = Class.forName("com.automate.formgen.cache.FormDatabaseCache", true, cl);
                Object cacheInst = cacheClass.getMethod("getInstance").invoke(null);
                Object descriptor = cacheClass.getMethod("getDealFieldDescriptor", int.class).invoke(cacheInst, fieldNum);
                if (descriptor != null) {
                    boolean isNum = (Boolean) descriptor.getClass().getMethod("isNumericField").invoke(descriptor);
                    return isNum ? "NUMERIC" : "TEXT";
                }
            } catch (Throwable ignored) {}
        }

        // Prompt / variable reference in open form's Prompts
        if (formGenInstance != null) {
            try {
                Method getOpenForm = formGenInstance.getClass().getDeclaredMethod("getOpenForm");
                getOpenForm.setAccessible(true);
                Object openForm = getOpenForm.invoke(formGenInstance);
                if (openForm != null) {
                    Object formData = openForm.getClass().getMethod("getFormData").invoke(openForm);
                    if (formData != null) {
                        Object prompts = formData.getClass().getMethod("getPrompts").invoke(formData);
                        if (prompts != null) {
                            Object promptData = prompts.getClass().getMethod("get", String.class).invoke(prompts, s.toUpperCase(Locale.ROOT));
                            if (promptData != null) {
                                Object respType = promptData.getClass().getMethod("getResponseType").invoke(promptData);
                                if (respType != null) {
                                    String rt = respType.toString().toUpperCase(Locale.ROOT);
                                    if (rt.contains("NUM") || rt.contains("DEC") || rt.contains("AMT") || rt.contains("CURRENCY") || rt.contains("INT")) {
                                        return "NUMERIC";
                                    }
                                }
                            }
                        }
                    }
                }
            } catch (Throwable ignored) {}
        }

        return "TEXT";
    }

    private static boolean isWrappedByOuterParens(String s) {
        if (s == null || s.length() < 2 || s.charAt(0) != '(' || s.charAt(s.length() - 1) != ')') return false;
        int depth = 0;
        boolean inQuotes = false;
        char quoteChar = 0;
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (inQuotes) {
                if (c == quoteChar) inQuotes = false;
                continue;
            }
            if (c == '"' || c == '\'') {
                inQuotes = true;
                quoteChar = c;
                continue;
            }
            if (c == '(') depth++;
            else if (c == ')') {
                depth--;
                if (depth == 0 && i < s.length() - 1) return false;
            }
        }
        return depth == 0;
    }

    private static boolean isValidIdentifier(String s) {
        if (s == null || s.isEmpty()) return false;
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (!(Character.isLetterOrDigit(c) || c == '_')) return false;
        }
        return true;
    }

    private static List<String> splitTopLevelArgs(String inner) {
        List<String> result = new ArrayList<String>();
        if (inner == null || inner.trim().isEmpty()) return result;
        int depth = 0;
        boolean inQuotes = false;
        char quoteChar = 0;
        int start = 0;
        for (int i = 0; i < inner.length(); i++) {
            char c = inner.charAt(i);
            if (inQuotes) {
                if (c == quoteChar) inQuotes = false;
                continue;
            }
            if (c == '"' || c == '\'') {
                inQuotes = true;
                quoteChar = c;
                continue;
            }
            if (c == '(') depth++;
            else if (c == ')') { if (depth > 0) depth--; }
            else if (c == ',' && depth == 0) {
                result.add(inner.substring(start, i).trim());
                start = i + 1;
            }
        }
        result.add(inner.substring(start).trim());
        return result;
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

                String snippetText = liveVariables.get("ActiveCodeSnippetText");
                if (snippetText != null && !snippetText.trim().isEmpty()) {
                    hasItems |= addVariableMenuItem(display, pasteSubMenu, itemCons, addListenerMethod, "Code Snippet", snippetText);
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
        if (!isFeatureEnabled("FormGenDialogAutoFill")) {
            removeEnhanceDialogShell(shell);
            return;
        }
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

    private static void removeEnhanceDialogShell(final Object shell) {
        if (shell == null || isDisposed(shell)) return;
        try {
            boolean removed = removeInjectedButtonsRecursively(shell);
            Method getDataMethod = shell.getClass().getMethod("getData", String.class);
            if (removed || Boolean.TRUE.equals(getDataMethod.invoke(shell, "SoleraEnhanced"))) {
                shell.getClass().getMethod("setData", String.class, Object.class).invoke(shell, "SoleraEnhanced", null);
                shell.getClass().getMethod("layout", boolean.class, boolean.class).invoke(shell, true, true);
            }
        } catch (Throwable ignored) {}
    }

    private static boolean removeInjectedButtonsRecursively(Object widget) {
        if (widget == null || isDisposed(widget)) return false;
        boolean anyRemoved = false;
        try {
            Method getDataMethod = widget.getClass().getMethod("getData", String.class);
            if (Boolean.TRUE.equals(getDataMethod.invoke(widget, "SoleraInjectedButton"))) {
                Object parent = null;
                try { parent = widget.getClass().getMethod("getParent").invoke(widget); } catch (Throwable ignored) {}
                try { widget.getClass().getMethod("dispose").invoke(widget); } catch (Throwable ignored) {}
                if (parent != null && !isDisposed(parent)) {
                    try {
                        Object layout = parent.getClass().getMethod("getLayout").invoke(parent);
                        if (layout != null) {
                            Field nc = layout.getClass().getField("numColumns");
                            int cols = nc.getInt(layout);
                            if (cols > 1) {
                                nc.setInt(layout, cols - 1);
                            }
                        }
                        parent.getClass().getMethod("layout", boolean.class, boolean.class).invoke(parent, true, true);
                    } catch (Throwable ignored) {}
                }
                return true;
            }

            Class<?> compositeClass = Class.forName("org.eclipse.swt.widgets.Composite");
            if (compositeClass.isInstance(widget)) {
                Method getChildrenMethod = compositeClass.getMethod("getChildren");
                Object[] children = (Object[]) getChildrenMethod.invoke(widget);
                if (children != null) {
                    for (Object c : children) {
                        if (removeInjectedButtonsRecursively(c)) {
                            anyRemoved = true;
                        }
                    }
                }
            }
        } catch (Throwable ignored) {}
        return anyRemoved;
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
            btnFill.getClass().getMethod("setData", String.class, Object.class).invoke(btnFill, "SoleraInjectedButton", Boolean.TRUE);

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
            btnRegen.getClass().getMethod("setData", String.class, Object.class).invoke(btnRegen, "SoleraInjectedButton", Boolean.TRUE);

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

            if (formPropsDlg != null) {
                shell.getClass().getMethod("setData", String.class, Object.class).invoke(shell, "SoleraFormPropsDlg", formPropsDlg);
            }
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
            btnOpenUtils.getClass().getMethod("setData", String.class, Object.class).invoke(btnOpenUtils, "SoleraInjectedButton", Boolean.TRUE);

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
