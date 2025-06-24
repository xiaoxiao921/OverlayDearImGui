using System;
using System.Collections.Generic;

namespace OverlayDearImGui;

internal sealed class DisposableList<U> : List<U>, IDisposable where U : IDisposable
{
    public DisposableList() { }
    public DisposableList(int capacity) : base(capacity) { }

    public void Dispose()
    {
        foreach (var u in this)
        {
            u.Dispose();
        }
        Clear();
    }
}