namespace Wormy.Core;

public sealed class NavigationHistory<T>(T initial, int capacity = 100) where T : notnull
{
    private readonly List<T> routes = [initial];
    private int position;
    public bool CanBack => position > 0;
    public bool CanForward => position + 1 < routes.Count;
    public T Current => routes[position];
    public void ReplaceCurrent(T current) => routes[position] = current;
    public void Visit(T route)
    {
        if (EqualityComparer<T>.Default.Equals(Current, route)) return;
        routes.RemoveRange(position + 1, routes.Count - position - 1);
        routes.Add(route); position++;
        if (routes.Count > Math.Max(2, capacity)) { routes.RemoveAt(0); position--; }
    }
    public T Back() { if (CanBack) position--; return Current; }
    public T Forward() { if (CanForward) position++; return Current; }
}
