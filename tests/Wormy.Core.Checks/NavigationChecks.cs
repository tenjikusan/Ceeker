using Wormy.Core;

internal static class NavigationChecks
{
    public static void Run()
    {
        var history = new NavigationHistory<string>("Home", 3);
        if (history.CanBack || history.CanForward) throw new Exception("Initial history enabled.");
        history.ReplaceCurrent("Home query"); history.Visit("Images"); history.Visit("Videos");
        if (history.Back() != "Images" || history.Back() != "Home query" || history.Back() != "Home query") throw new Exception("Back or query snapshot failed.");
        if (history.Forward() != "Images") throw new Exception("Forward failed.");
        history.Visit("Images"); if (!history.CanForward) throw new Exception("Same route removed forward history.");
        history.Visit("Documents"); if (history.CanForward) throw new Exception("Branch retained future route.");
        history.Visit("Music"); history.Back();
        if (history.Back() != "Images" || history.CanBack) throw new Exception("History capacity failed.");
        Console.WriteLine("PASS: navigation snapshots, back/forward boundaries, same-route reuse, branching and capacity.");
    }
}
