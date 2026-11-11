namespace SmokeDotnet;

public static class GreetingTests
{
    public static void TestGreetNonEmpty()
    {
        if (string.IsNullOrEmpty(Hello.Greet()))
        {
            throw new InvalidOperationException("Greet() returned an empty string");
        }
    }
}
