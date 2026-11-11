namespace SmokeDotnet;

public static class Hello
{
    public static string Greet() => "Hello";

    public static int Run(string[] args)
    {
        if (args is ["--test", var testName])
        {
            var test = typeof(Hello).Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static))
                .SingleOrDefault(method => method.Name == testName);

            if (test is null)
            {
                Console.Error.WriteLine($"Test not found: {testName}");
                return 2;
            }

            try
            {
                test.Invoke(null, null);
                return 0;
            }
            catch (System.Reflection.TargetInvocationException exception)
            {
                Console.Error.WriteLine(exception.InnerException?.Message ?? exception.Message);
                return 1;
            }
        }

        Console.WriteLine(Greet());
        return 0;
    }
}
