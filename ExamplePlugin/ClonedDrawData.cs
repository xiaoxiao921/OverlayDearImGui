using System;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace OverlayDearImGui;

internal unsafe sealed class ClonedDrawData : IDisposable
{
    public ImDrawData* Data { get; private set; }

    public unsafe ClonedDrawData(ImDrawDataPtr inp)
    {
        long ddsize = Marshal.SizeOf<ImDrawData>();

        // start with a shallow copy
        Data = (ImDrawData*)ImGui.MemAlloc((uint)ddsize);
        Buffer.MemoryCopy(inp, Data, ddsize, ddsize);

        // clone the draw data
        int numLists = inp.CmdLists.Size;
        var cmdListPtrs = ImGui.MemAlloc((uint)(Marshal.SizeOf<IntPtr>() * numLists));
        Data->CmdLists = new ImVector(
            numLists,
            numLists,
            cmdListPtrs);
        for (int i = 0; i < inp.CmdLists.Size; ++i)
        {
            Data->CmdLists.Ref<ImDrawListPtr>(i) = inp.CmdLists[i].CloneOutput();
        }
    }

    public unsafe void Dispose()
    {
        if (Data == null)
            return;

        for (int i = 0; i < Data->CmdListsCount; ++i)
        {
            Data->CmdLists.Ref<ImDrawListPtr>(i).Destroy();
        }
        ImGuiNative.ImDrawData_destroy(Data);
        Data = null;
    }
}
