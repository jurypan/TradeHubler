using System.ComponentModel;
using System.Globalization;
using System.IO;

namespace JCTG.Client
{
    public static class Helpers
    {
        public static void Print(object obj, bool color = false)
        {
            if (color == true)
            {
                // Set both foreground (text) color and background color
                Console.ForegroundColor = ConsoleColor.Red;
                Console.BackgroundColor = ConsoleColor.Yellow;

                // Write a line of text in yellow with blue background
                Console.WriteLine(obj);

                // Reset the colors to their default values
                Console.ResetColor();
            }
            else
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

        public static async Task TryDeleteFileAsync(string path)
        {
            int maxAttempts = 6;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    File.Delete(path);
                    break; // Success, exit the loop
                }
                catch (IOException)
                {
                    if (attempt != maxAttempts)
                        await Task.Delay(500); // Wait before retrying
                }
            }
        }

        public static string Format(double value)
        {
            return value.ToString("G", CultureInfo.CreateSpecificCulture("en-US"));
        }

        public static string Format(decimal value)
        {
            return value.ToString("G", CultureInfo.CreateSpecificCulture("en-US"));
        }

        public static async Task<string> TryReadFileAsync(string path)
        {
            int maxAttempts = 6;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    return await File.ReadAllTextAsync(path);
                }
                catch (IOException)
                {
                    if (attempt == maxAttempts) return string.Empty;
                    await Task.Delay(500); // Wait before retrying
                }
            }
            return string.Empty; // Should not reach here
        }

        public static async Task TryWriteFileAsync(string fileName, string json)
        {
            int maxAttempts = 6;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await File.WriteAllTextAsync(fileName, json);
                    break; // Success, exit the loop
                }
                catch (IOException)
                {
                    if (attempt != maxAttempts)
                        await Task.Delay(500); // Wait before retrying
                }
            }
        }

        public static string GetDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            if (field != null)
                return Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is not DescriptionAttribute attribute ? value.ToString() : attribute.Description;
            else
                return string.Empty;
        }
    }
}
