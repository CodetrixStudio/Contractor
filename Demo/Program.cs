using System;
using Contractor;

namespace Demo
{
    class Program
    {
        static void Main(string[] args)
        {
            IBoring boring = new MyClass();
            boring.TryRefactoringMe = 2;
            Console.WriteLine("Hello World!");
        }        
    }

    [AutoImplement]
    interface IBoring
    {
        public int TryRefactoringMe { get; set; }
    }

    partial class MyClass : IBoring
    {

    }
}
