using System;

namespace OkCNaggerBot
{
    /// <summary>
    /// Description of Singleton1
    /// </summary>
    public sealed class Singleton1
    {
        private static Singleton1 instance = new Singleton1();

        public static Singleton1 Instance
        {
            get
            {
                return instance;
            }
        }

        private Singleton1()
        {
        }
    }
}
