namespace Servora.Web.Services;

/// <summary>Circuit-scoped UI state shared between layout components (sidebar, command palette, page title).</summary>
public sealed class UiStateService
{
    /// <summary>Raised when any state changed.</summary>
    public event Action? Changed;

    /// <summary>Gets a value indicating whether the sidebar is collapsed.</summary>
    public bool SidebarCollapsed { get; private set; }

    /// <summary>Gets a value indicating whether the mobile sidebar overlay is open.</summary>
    public bool MobileSidebarOpen { get; private set; }

    /// <summary>Gets a value indicating whether the command palette is open.</summary>
    public bool PaletteOpen { get; private set; }

    /// <summary>Sets the sidebar collapsed state.</summary>
    /// <param name="collapsed">The new state.</param>
    public void SetSidebarCollapsed(bool collapsed)
    {
        SidebarCollapsed = collapsed;
        Changed?.Invoke();
    }

    /// <summary>Toggles the mobile sidebar overlay.</summary>
    public void ToggleMobileSidebar()
    {
        MobileSidebarOpen = !MobileSidebarOpen;
        Changed?.Invoke();
    }

    /// <summary>Closes the mobile sidebar overlay.</summary>
    public void CloseMobileSidebar()
    {
        if (MobileSidebarOpen)
        {
            MobileSidebarOpen = false;
            Changed?.Invoke();
        }
    }

    /// <summary>Opens or closes the command palette.</summary>
    /// <param name="open">The new state.</param>
    public void SetPaletteOpen(bool open)
    {
        PaletteOpen = open;
        Changed?.Invoke();
    }
}
