using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TAIAMESControlServer
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new FormMain());
            var context = new MyApplicationContext(new FormMain());

            // Run the application with the specific context.
            Application.Run(context);
        }
    }

    class MyApplicationContext : ApplicationContext
    {
        public static MyApplicationContext CurrentContext;

        public MyApplicationContext(Form mainForm)
            : base(mainForm)
        {
            //...implement any hooks, additional context etc.

            CurrentContext = this;
        }
    }
}
