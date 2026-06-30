using System;
namespace NUnit.Framework
{
    public class TestFixtureAttribute : Attribute {}
    public class TestAttribute : Attribute {}
    public class Assert
    {
        public static void That(bool condition, string message = "") {}
    }
}
