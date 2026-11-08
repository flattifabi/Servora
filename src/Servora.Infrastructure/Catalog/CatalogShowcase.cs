namespace Servora.Infrastructure.Catalog;

/// <summary>The showcase catalog range seeded once by <see cref="CatalogSeeder"/> (realistic demo data for the portal).</summary>
internal static class CatalogShowcase
{
    /// <summary>Hardware items (together with the business laptop and the mobile phone: 24 services).</summary>
    public static readonly IReadOnlyList<CatalogShowcaseItem> Hardware =
    [
        new("Developer laptop", "laptop", "Apple MacBook Pro 14\" M4 Pro", "Maximum performance for software development, data and design work.", 69m, true, 36, 7, "5–7 working days", "HW-002", 20),
        new("Ultralight laptop", "laptop", "Dell XPS 13", "Light and compact for people who travel a lot.", 45m, true, 36, 5, "3–5 working days", "HW-003", 22),
        new("Desktop workstation", "monitor", "HP Z2 Mini G1i", "Compact workstation for CAD, video and analytics workloads.", 49m, true, 36, 7, "5–7 working days", "HW-004", 24),
        new("27\" monitor", "monitor", "Dell UltraSharp U2724D", "Sharp QHD display with USB-C – one cable to your laptop.", 9m, true, 36, 3, "2–3 working days", "HW-005", 40),
        new("34\" curved monitor", "monitor", "LG 34WQ75C", "Ultra-wide screen for spreadsheets, timelines and side-by-side work.", 14m, true, 36, 5, "3–5 working days", "HW-006", 42),
        new("USB-C docking station", "plug", "Lenovo ThinkPad Universal USB-C Dock", "Connect monitors, network and power with a single cable.", 5m, true, 36, 3, "2–3 working days", "HW-007", 44),
        new("Monitor arm", "maximize", "Ergotron LX", "Height and angle adjustable mount for one monitor.", 149m, false, null, 5, "3–5 working days", "HW-008", 46),
        new("Headset", "message-circle", "Jabra Evolve2 65", "Wireless headset with noise cancellation for calls and meetings.", 189m, false, null, 3, "2–3 working days", "HW-009", 50),
        new("Webcam", "eye", "Logitech Brio 505", "Full HD webcam with auto light correction for video calls.", 129m, false, null, 3, "2–3 working days", "HW-010", 52),
        new("Conference speakerphone", "users", "Jabra Speak2 75", "Portable speakerphone for small meeting rooms.", 249m, false, null, 5, "3–5 working days", "HW-011", 54),
        new("Keyboard and mouse set", "command", "Logitech MX Keys S Combo", "Comfortable wireless keyboard and mouse.", 119m, false, null, 3, "2–3 working days", "HW-012", 60),
        new("Ergonomic mouse", "target", "Logitech MX Vertical", "Vertical mouse that reduces strain on the wrist.", 99m, false, null, 3, "2–3 working days", "HW-013", 62),
        new("Tablet", "phone", "Apple iPad Air 11\"", "Tablet with keyboard for notes, presentations and approvals on the go.", 25m, true, 36, 5, "3–5 working days", "HW-014", 70),
        new("Smartphone accessories", "phone", "Case and screen protector", "Protection for your company smartphone.", 35m, false, null, 2, "1–2 working days", "HW-015", 72),
        new("Laptop bag", "box", "Targus 14\" backpack", "Padded backpack for laptops up to 14 inches.", 49m, false, null, 3, "2–3 working days", "HW-016", 80),
        new("USB-C power adapter", "zap", "65 W USB-C charger", "Additional charger for your home office or travel.", 39m, false, null, 2, "1–2 working days", "HW-017", 82),
        new("Presentation clicker", "play", "Logitech Spotlight", "Wireless presenter with digital highlighting.", 45m, false, null, 2, "1–2 working days", "HW-018", 84),
        new("External SSD", "hard-drive", "Samsung T7 1 TB, encrypted", "Fast, encrypted portable storage for large files.", 99m, false, null, 3, "2–3 working days", "HW-019", 86),
        new("Label printer", "tag", "Brother P-touch", "Label printer for shelves, cables and devices.", 99m, false, null, 5, "3–5 working days", "HW-021", 88),
        new("Height-adjustable desk", "building", "Electric sit-stand desk", "Ergonomic desk for your workplace or home office.", 15m, true, 36, 15, "2–3 weeks", "HW-022", 90),
        new("Replacement laptop battery", "zap", "Original manufacturer battery", "Replacement for a battery that no longer holds its charge.", 0m, false, null, 3, "2–3 working days", "HW-023", 92),
        new("Device return", "refresh", null, "Return a device you no longer need – we collect and wipe it securely.", 0m, false, null, 5, "Pick-up within 5 working days", "HW-024", 99),
    ];

    /// <summary>Software items.</summary>
    public static readonly IReadOnlyList<CatalogShowcaseItem> Software =
    [
        new("Adobe Acrobat Pro", "file-text", "Adobe Acrobat Pro (annual)", "Create, edit, sign and protect PDF documents.", 16m, true, 12, 1, "1 working day", "SW-002", 20),
        new("Adobe Creative Cloud", "sparkles", "All Apps", "Photoshop, Illustrator, InDesign, Premiere Pro and more.", 75m, true, 12, 2, "2 working days", "SW-003", 22),
        new("Microsoft Visio", "workflow", "Visio Plan 2", "Diagrams, flowcharts and floor plans.", 14m, true, 12, 1, "1 working day", "SW-004", 24),
        new("Microsoft Project", "calendar", "Project Plan 3", "Plan projects, resources and timelines.", 28m, true, 12, 1, "1 working day", "SW-005", 26),
        new("Power BI Pro", "bar-chart", null, "Build and share interactive reports and dashboards.", 13m, true, 12, 1, "1 working day", "SW-006", 28),
        new("JetBrains All Products Pack", "code", null, "IntelliJ IDEA, Rider, PyCharm, WebStorm and more.", 25m, true, 12, 1, "1 working day", "SW-007", 40),
        new("Figma", "layers", "Professional seat", "Collaborative interface design and prototyping.", 15m, true, 12, 1, "1 working day", "SW-008", 42),
        new("Miro", "grid", "Business seat", "Online whiteboard for workshops and brainstorming.", 8m, true, 12, 1, "1 working day", "SW-009", 44),
        new("Password manager", "lock", "1Password Business", "Store and share passwords securely.", 4m, true, 12, 0, "Available immediately", "SW-011", 46),
    ];

    /// <summary>Access and permission items.</summary>
    public static readonly IReadOnlyList<CatalogShowcaseItem> Access =
    [
        new("Shared drive access", "folder", null, "Read or write access to a department or project share.", 0m, false, null, 1, "1 working day", "AC-003", 30),
        new("Temporary admin rights", "key", "Local administrator, max. 7 days", "Install or configure software yourself for a limited time.", 0m, false, null, 1, "1 working day", "AC-004", 32),
        new("SAP role", "id-card", null, "Request an additional SAP role or authorization.", 0m, false, null, 3, "2–3 working days", "AC-005", 34),
        new("Security token", "shield", "YubiKey 5 NFC", "Hardware key for phishing-resistant multi-factor sign-in.", 25m, false, null, 3, "2–3 working days", "AC-006", 36),
        new("Guest Wi-Fi access", "globe", null, "Wi-Fi vouchers for visitors and external partners.", 0m, false, null, 0, "Available immediately", "AC-007", 38),
        new("Distribution list membership", "mail", null, "Join or leave an e-mail distribution list.", 0m, false, null, 1, "1 working day", "AC-008", 40),
    ];

    /// <summary>IT support items.</summary>
    public static readonly IReadOnlyList<CatalogShowcaseItem> Support =
    [
        new("On-site IT appointment", "calendar-clock", null, "A technician comes to your desk at a time that suits you.", 0m, false, null, 2, "Within 2 working days", "SUP-002", 20),
        new("Device repair", "wrench", null, "Have a defective device checked and repaired.", 0m, false, null, 5, "3–5 working days", "SUP-003", 22),
        new("Data recovery", "database", null, "Restore deleted files from OneDrive, SharePoint or backups.", 0m, false, null, 1, "1 working day", "SUP-004", 24),
        new("Workplace move", "map-pin", null, "Move your devices and phone to a new desk or office.", 0m, false, null, 5, "Plan 5 working days ahead", "SUP-005", 26),
        new("IT training session", "lightbulb", "1:1 or team session", "Get to know Teams, OneDrive, security basics and more.", 0m, false, null, 5, "Scheduled with you", "SUP-006", 28),
    ];
}
