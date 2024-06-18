namespace GridifyExtensions.Abstractions;

public interface IOrderThenBy
{
    IOrderThenBy ThenBy(string column);
    IOrderThenBy ThenByDescending(string column);
}
