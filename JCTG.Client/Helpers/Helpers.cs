using System.Globalization;
using System.IO;


/*Helpers class

This class includes helper functions for printing, formatting and file operations. 

*/

namespace JCTG.Client
{
    public class Helpers
    {
		public static void Print(object obj)
        {
            Console.WriteLine(obj);
        }

        public static async Task<bool> TryWriteToFileAsync(string filePath, string text)
        {
            try
            {
                await File.WriteAllTextAsync(filePath, text);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryWriteToFile(string filePath, string text)
        {
            try
            {
                File.WriteAllText(filePath, text);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }
		
        public static void TryDeleteFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }

        public static string Format(double value)
        {
            return value.ToString("G", CultureInfo.CreateSpecificCulture("en-US"));
        }

        public static async Task<string> TryReadFileAsync(string path)
        {
            try
            {
                return await File.ReadAllTextAsync(path);
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
