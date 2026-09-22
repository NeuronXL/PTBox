namespace PTBox.Launcher.Services;

// Input providers (keyboard today, XInput later) emit these semantic actions.
public enum NavigationDirection { Left, Right, Up, Down }
public readonly record struct NavigationTarget(int Index, int Row, int Column);
public sealed class NavigationService
{
    public int Move(IReadOnlyList<NavigationTarget> targets, int selected, NavigationDirection direction)
    {
        var current = targets.FirstOrDefault(x => x.Index == selected);
        var candidates = targets.Where(x => direction switch
        {
            NavigationDirection.Left => x.Row == current.Row && x.Column < current.Column,
            NavigationDirection.Right => x.Row == current.Row && x.Column > current.Column,
            NavigationDirection.Up => x.Row < current.Row,
            NavigationDirection.Down => x.Row > current.Row,
            _ => false
        });
        return candidates.OrderBy(x => direction is NavigationDirection.Left or NavigationDirection.Right
                ? Math.Abs(x.Column - current.Column) : Math.Abs(x.Row - current.Row))
            .ThenBy(x => Math.Abs(x.Column - current.Column)).Select(x => (int?)x.Index).FirstOrDefault() ?? selected;
    }
    public static List<NavigationTarget> CreateTargets(int cards, int footer, int columns = 3)
    {
        var targets = Enumerable.Range(0, cards).Select(i => new NavigationTarget(i, i / columns, i % columns)).ToList();
        var footerRow = (cards + columns - 1) / columns;
        targets.AddRange(Enumerable.Range(0, footer).Select(i => new NavigationTarget(cards + i, footerRow, i)));
        return targets;
    }
}
