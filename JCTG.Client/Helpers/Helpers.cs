using System.ComponentModel;
using System.Globalization;

namespace JCTG.Client
{
    public static class Helpers
    {

        public static void Print(object obj, bool color = false)
        {
            if(color == true) 
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

        public static string Format(decimal value)
        {
            return value.ToString("G", CultureInfo.CreateSpecificCulture("en-US"));
        }

        public static async Task<string> TryReadFileAsync(string path)
        {
            try
            {
                return await File.ReadAllTextAsync(path);
            }
            catch (IOException ex)
            {
                if(ex.Message.Contains("Could not find file"))
                {
                    try
                    {
                        File.Create(path).Close();
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string GetDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;

            return attribute == null ? value.ToString() : attribute.Description;
        }

        public static T GetValueFromDescription<T>(string description) where T : Enum
        {
            foreach (var field in typeof(T).GetFields())
            {
                if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
                {
                    if (attribute.Description == description)
                        return (T)field.GetValue(null);
                }
                else
                {
                    if (field.Name == description)
                        return (T)field.GetValue(null);
                }
            }

            throw new ArgumentException("Not found.", nameof(description));
        }
    }
}
