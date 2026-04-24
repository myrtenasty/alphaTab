using System;

namespace AlphaTab;

internal class DelegatedEventEmitter : IEventEmitter
{
    private readonly Action<Action> _on;
    private readonly Action<Action> _off;

    public DelegatedEventEmitter(Action<Action> on, Action<Action> off)
    {
        _on = on;
        _off = off;
    }

    public Action On(Action value)
    {
        _on(value);
        return () => _off(value);
    }

    public void Off(Action value)
    {
        _off(value);
    }
}

internal class DelegatedEventEmitter<T> : IEventEmitterOfT<T>
{
    private readonly Action<Action<T>> _on;
    private readonly Action<Action<T>> _off;

    public DelegatedEventEmitter(Action<Action<T>> on, Action<Action<T>> off)
    {
        _on = on;
        _off = off;
    }

    public Action On(Action value)
    {
        return () => { };
    }

    public void Off(Action value)
    {
    }

    public Action On(Action<T> value)
    {
        _on(value);
        return () => _off(value);
    }

    public void Off(Action<T> value)
    {
        _off(value);
    }
}