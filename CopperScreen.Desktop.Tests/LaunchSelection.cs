using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using CopperScreen;

namespace CopperScreen.Tests;

internal static class LaunchSelection
{
    private static ComboBox Selector(Window window, string name) => window.GetLogicalDescendants()
        .OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == name);

    public static void Select(Window window, int choice)
    {
        var method = choice >= 6 ? "MinimalDiskBoot" : choice >= 3 ? "CopperStart" : "KickstartRom";
        var boot = Selector(window, "Boot method");
        Assert.Contains(method, boot.Items); // Do not silently test another setup when a build omits this method.
        // Select a portable method before changing models to leave unsupported saved combinations.
        boot.SelectedItem = "KickstartRom";
        Selector(window, "Amiga model").SelectedIndex = choice >= 6 ? choice - 6 : choice >= 4 ? choice - 4 : Math.Min(choice, 2);
        boot.SelectedItem = method;
    }

    public static int Current(Window window) => Selector(window, "Boot method").SelectedItem is string method
        ? CopperScreenBootMethods.Choice((CopperScreenMachineModel)Selector(window, "Amiga model").SelectedIndex,
            Enum.Parse<CopperScreenBootMethod>(method)) : -1;
}
