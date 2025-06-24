using System;
using System.Threading;
using BepInEx;

namespace OverlayDearImGui;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class OverlayDearImGui : BaseUnityPlugin
{
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "iDeathHD";
    public const string PluginName = "OverlayDearImGui";
    public const string PluginVersion = "2.0.0";

    private static Thread _renderThread;

    public void Awake()
    {
        Log.Init(Logger);

        _renderThread = new Thread(() =>
        {
            try
            {
                new Overlay().Render("Risk of Rain 2", "UnityWndClass");
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        });
        _renderThread.Start();
    }
}
