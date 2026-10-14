// PedalKeys for macOS: a menu-bar app that turns a Sanguine Preon / VEC Infinity USB foot pedal
// (HID 05F3:00FF) into ordinary key presses for whichever app has focus.
//
//   Left   -> Left arrow   (repeats every 200 ms while held)
//   Center -> Space        (once per press)
//   Right  -> Right arrow  (repeats every 200 ms while held)
//
// Build with helper-mac/build.sh (needs Xcode command line tools).

import Cocoa
import IOKit.hid
import ServiceManagement

private let pedalVendorID = 0x05F3
private let pedalProductID = 0x00FF
private let repeatInterval: TimeInterval = 0.2
private let loggingEnabled = CommandLine.arguments.contains("--log")

/// Opt-in diagnostics: `PedalKeys --log` appends raw button events to ~/Library/Logs/PedalKeys.log
private func debugLog(_ text: String) {
    guard loggingEnabled else { return }
    let url = FileManager.default.homeDirectoryForCurrentUser
        .appendingPathComponent("Library/Logs/PedalKeys.log")
    let line = "\(Date())  \(text)\n"
    if let handle = try? FileHandle(forWritingTo: url) {
        handle.seekToEndOfFile()
        handle.write(Data(line.utf8))
        try? handle.close()
    } else {
        try? line.write(to: url, atomically: true, encoding: .utf8)
    }
}

// MARK: - Pedal buttons and keys

/// The pedal reports three buttons on the HID Button page; usage 1, 2, 3 = left, center, right.
private enum Pedal: Int {
    case left = 1, center = 2, right = 3

    var keyCode: CGKeyCode {
        switch self {
        case .left: return 123   // kVK_LeftArrow
        case .center: return 49  // kVK_Space
        case .right: return 124  // kVK_RightArrow
        }
    }

    var repeatsWhileHeld: Bool { self != .center }
}

/// Posts a key press and release to the system, so the focused app sees a normal keypress.
private func tap(_ code: CGKeyCode) {
    let source = CGEventSource(stateID: .hidSystemState)
    CGEvent(keyboardEventSource: source, virtualKey: code, keyDown: true)?.post(tap: .cghidEventTap)
    CGEvent(keyboardEventSource: source, virtualKey: code, keyDown: false)?.post(tap: .cghidEventTap)
}

// MARK: - Device handling

private final class PedalController {
    private let manager = IOHIDManagerCreate(kCFAllocatorDefault, IOOptionBits(kIOHIDOptionsTypeNone))
    private var held = Set<Int>()
    private var timers = [Int: Timer]()

    private(set) var deviceCount = 0
    private(set) var inputBlocked = false
    var onChange: (() -> Void)?

    var paused = false {
        didSet { if paused { releaseAll() }; onChange?() }
    }

    func start() {
        // macOS requires explicit permission (Input Monitoring) before an app may read input devices.
        _ = IOHIDRequestAccess(kIOHIDRequestTypeListenEvent)

        let match: [String: Any] = ["VendorID": pedalVendorID, "ProductID": pedalProductID]
        IOHIDManagerSetDeviceMatching(manager, match as CFDictionary)

        let context = Unmanaged.passUnretained(self).toOpaque()
        IOHIDManagerRegisterDeviceMatchingCallback(manager, { context, _, _, _ in
            guard let context = context else { return }
            Unmanaged<PedalController>.fromOpaque(context).takeUnretainedValue().deviceAdded()
        }, context)
        IOHIDManagerRegisterDeviceRemovalCallback(manager, { context, _, _, _ in
            guard let context = context else { return }
            Unmanaged<PedalController>.fromOpaque(context).takeUnretainedValue().deviceRemoved()
        }, context)
        IOHIDManagerRegisterInputValueCallback(manager, { context, _, _, value in
            guard let context = context else { return }
            Unmanaged<PedalController>.fromOpaque(context).takeUnretainedValue().handle(value)
        }, context)

        IOHIDManagerScheduleWithRunLoop(manager, CFRunLoopGetMain(), CFRunLoopMode.defaultMode.rawValue)
        // The manager keeps watching for the device, so plugging in late or replugging just works.
        let result = IOHIDManagerOpen(manager, IOOptionBits(kIOHIDOptionsTypeNone))
        inputBlocked = (result == kIOReturnNotPermitted)
        onChange?()
    }

    fileprivate func deviceAdded() {
        deviceCount += 1
        debugLog("pedal connected (\(deviceCount))")
        onChange?()
    }

    fileprivate func deviceRemoved() {
        deviceCount = max(0, deviceCount - 1)
        debugLog("pedal disconnected (\(deviceCount))")
        if deviceCount == 0 { releaseAll() }
        onChange?()
    }

    fileprivate func handle(_ value: IOHIDValue) {
        let element = IOHIDValueGetElement(value)
        let page = IOHIDElementGetUsagePage(element)
        let usage = IOHIDElementGetUsage(element)
        let state = IOHIDValueGetIntegerValue(value)
        debugLog("page=\(page) usage=\(usage) value=\(state)")

        guard page == 0x09, let pedal = Pedal(rawValue: Int(usage)) else { return }
        if state != 0 { press(pedal) } else { release(pedal) }
    }

    private func press(_ pedal: Pedal) {
        guard !paused, !held.contains(pedal.rawValue) else { return }
        held.insert(pedal.rawValue)
        tap(pedal.keyCode)
        guard pedal.repeatsWhileHeld else { return }
        // First repeat after one interval, then steady.
        timers[pedal.rawValue] = Timer.scheduledTimer(withTimeInterval: repeatInterval, repeats: true) {
            [weak self] _ in
            guard let self = self, !self.paused, self.held.contains(pedal.rawValue) else { return }
            tap(pedal.keyCode)
        }
    }

    private func release(_ pedal: Pedal) {
        held.remove(pedal.rawValue)
        timers.removeValue(forKey: pedal.rawValue)?.invalidate()
    }

    private func releaseAll() {
        held.removeAll()
        timers.values.forEach { $0.invalidate() }
        timers.removeAll()
    }
}

// MARK: - Menu bar app

private final class AppDelegate: NSObject, NSApplicationDelegate {
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    private let controller = PedalController()
    private let statusLine = NSMenuItem(title: "", action: nil, keyEquivalent: "")
    private let permissionItem = NSMenuItem(
        title: "Allow keyboard control...", action: #selector(openAccessibilitySettings), keyEquivalent: "")
    private let pauseItem = NSMenuItem(title: "Pause mappings", action: #selector(pause), keyEquivalent: "")
    private let resumeItem = NSMenuItem(title: "Resume mappings", action: #selector(resume), keyEquivalent: "")
    private let loginItem = NSMenuItem(title: "Start at login", action: #selector(toggleLogin), keyEquivalent: "")
    private var permissionTimer: Timer?

    func applicationDidFinishLaunching(_ notification: Notification) {
        // One copy at a time.
        if let id = Bundle.main.bundleIdentifier,
           NSRunningApplication.runningApplications(withBundleIdentifier: id).count > 1 {
            NSApp.terminate(nil)
            return
        }

        let menu = NSMenu()
        statusLine.isEnabled = false
        for item in [permissionItem, pauseItem, resumeItem, loginItem] { item.target = self }
        menu.addItem(statusLine)
        menu.addItem(permissionItem)
        menu.addItem(.separator())
        menu.addItem(pauseItem)
        menu.addItem(resumeItem)
        menu.addItem(loginItem)
        menu.addItem(.separator())
        menu.addItem(NSMenuItem(title: "Quit", action: #selector(quit), keyEquivalent: "q"))
        menu.items.last?.target = self
        statusItem.menu = menu

        applyLoginDefaultOnFirstRun()

        controller.onChange = { [weak self] in self?.refresh() }

        // Posting key presses to other apps needs the Accessibility permission. Ask once.
        if !AXIsProcessTrusted() {
            _ = AXIsProcessTrustedWithOptions(["AXTrustedCheckOptionPrompt": true] as CFDictionary)
        }
        controller.start()

        // Pick up the permission being granted without restarting the app.
        permissionTimer = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in
            self?.refresh()
        }
        refresh()
    }

    private func refresh() {
        let trusted = AXIsProcessTrusted()
        let text: String
        let symbol: String
        let color: NSColor
        if !trusted {
            text = "Needs Accessibility permission"; symbol = "!"; color = .systemOrange
        } else if controller.inputBlocked {
            text = "Needs Input Monitoring permission"; symbol = "!"; color = .systemOrange
        } else if controller.paused {
            text = "Paused"; symbol = "||"; color = .secondaryLabelColor
        } else if controller.deviceCount > 0 {
            text = "Pedal connected"; symbol = "\u{25CF}"; color = .systemGreen
        } else {
            text = "Pedal not found. Retrying..."; symbol = "\u{25CB}"; color = .systemOrange
        }

        statusLine.title = text
        permissionItem.isHidden = trusted && !controller.inputBlocked
        pauseItem.isHidden = controller.paused
        resumeItem.isHidden = !controller.paused
        loginItem.state = (SMAppService.mainApp.status == .enabled) ? .on : .off

        if let button = statusItem.button {
            button.attributedTitle = NSAttributedString(
                string: "Pedal \(symbol)",
                attributes: [.foregroundColor: color, .font: NSFont.menuBarFont(ofSize: 0)])
            button.toolTip = "PedalKeys: \(text)"
        }
    }

    // "Start at login" is on by default. Apply that exactly once so turning it off sticks.
    private func applyLoginDefaultOnFirstRun() {
        let defaults = UserDefaults.standard
        guard !defaults.bool(forKey: "initialized") else { return }
        defaults.set(true, forKey: "initialized")
        try? SMAppService.mainApp.register()
    }

    @objc private func pause() { controller.paused = true }
    @objc private func resume() { controller.paused = false }

    @objc private func toggleLogin() {
        do {
            if SMAppService.mainApp.status == .enabled {
                try SMAppService.mainApp.unregister()
            } else {
                try SMAppService.mainApp.register()
            }
        } catch {
            debugLog("login item change failed: \(error)")
        }
        refresh()
    }

    @objc private func openAccessibilitySettings() {
        let pane = controller.inputBlocked && AXIsProcessTrusted() ? "Privacy_ListenEvent" : "Privacy_Accessibility"
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?\(pane)") {
            NSWorkspace.shared.open(url)
        }
    }

    @objc private func quit() { NSApp.terminate(nil) }
}

let app = NSApplication.shared
private let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.accessory) // menu bar only, no Dock icon
app.run()
