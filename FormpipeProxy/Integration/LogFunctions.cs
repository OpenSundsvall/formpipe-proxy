using System;
using FormpipeProxy.Models;

namespace FormpipeProxy.Integration
{
    public class LogFunctions
    {
        public void FormPipeErrorLog(string step, ErrorDetails er)
        {
            string text =
                "Step: " + step + Environment.NewLine +
                "Phase: " + er.Phase + Environment.NewLine +
                "Error Code: " + Convert.ToString(er.ErrorCode) + Environment.NewLine +
                "Error ID: " + Convert.ToString(er.ErrorId) + Environment.NewLine +
                "Error Message: " + er.ErrorMessage + Environment.NewLine +
                Environment.NewLine +
                "-------------------------------------------------------------------------";

            using (System.IO.StreamWriter file = new System.IO.StreamWriter("C:\\inetpub\\logs\\FormPipeLog\\prod\\log.txt", true))
            {
                file.WriteLine(text);
            }
        }

        public void ErrorLog(string text, Exception ex)
        {
            string filePath = "C:\\inetpub\\logs\\FormPipeLog\\prod\\error.txt";

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(filePath, true))
            {
                file.WriteLine("-----------------------------------------------------------------------------");
                file.WriteLine("Date : " + DateTime.Now.ToString());
                file.WriteLine();
                file.WriteLine(text);
                file.WriteLine();

                while (ex != null)
                {
                    file.WriteLine(ex.GetType().FullName);
                    file.WriteLine("Message : " + ex.Message);
                    file.WriteLine("StackTrace : " + ex.StackTrace);

                    ex = ex.InnerException;
                }
            }
        }
    }
}