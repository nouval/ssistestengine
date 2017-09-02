using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ssistestengine
{
    public class MainClass
    {
        public static void Main(string[] args)
        {
            new Recipe().Validate("recipe.yaml", "testfile.txt");
            new Recipe().Validate("recipe_csv.yaml", "testfile_csv.txt");
            new Recipe().Validate("recipe_pipe.yaml", "testfile_pipe.txt");
        }
    }

    public interface LayoutValidator
    {
        bool Validate(string line, Layout forIngredient);
    }

    public interface SpecificationValidator
    {
        bool Validate(string line, ref int offset, Specification forMeasurement);
    }

    public class FixedLayoutValidator : LayoutValidator
    {
        public bool Validate(string line, Layout forIngredient)
        {
			bool valid = false;
			int offset = 0;

			foreach (var spec in forIngredient.Specs)
			{
				valid = spec.Validate(line, ref offset);
				if (!valid)
					break;
			}

			return valid;
		}
    }

    public class RegexLayoutValidator : LayoutValidator
    {
        const string Config_RegexKey = "regex";
        const string QCSVRegex = "((?<=\\\")[^\\\"]*(?=\\\"(,|$)+)|(?<=,|^)[^,\\\"]*(?=,|$))";

        private string[] Split(string line, string regex)
        {
            var fields = new List<string>();
            var matchResults = new Regex(regex == null ? RegexLayoutValidator.QCSVRegex : regex).Matches(line);
			foreach (var match in matchResults)
			{
				fields.Add(match.ToString());
			}

            return fields.ToArray();
		}

        public bool Validate(String line, Layout layout)
        {
			bool valid = false;
            int offset = 0, index = 0;
            List<string> field2s = new List<string>();
            var fields = this.Split(line, layout.Configs.FindValue(RegexLayoutValidator.Config_RegexKey));

			foreach (var spec in layout.Specs)
			{
                valid = spec.Validate(fields[index], ref offset);
                offset = 0;
                index++;
				if (!valid)
					break;
			}

			return valid;
		}
    }

    public class StringValidator : SpecificationValidator
    {
        public bool Validate(string line, ref int offset, Specification forMeasurement)
        {
			bool valid = false;

			if (forMeasurement.Value != null)
			{
				valid = line.Length >= offset + forMeasurement.Value.Length && line.Substring(offset, forMeasurement.Value.Length).Equals(forMeasurement.Value);
				offset += forMeasurement.Value.Length;
			}
			else if (forMeasurement.Length != 0)
			{
				valid = line.Length >= offset + forMeasurement.Length && line.Substring(offset, forMeasurement.Length) != null;
				offset += forMeasurement.Length;
			}
            else
            {
                valid = !string.IsNullOrWhiteSpace(line);
            }

			return valid;            
        }
    }

	public class DateTimeValidator : SpecificationValidator
	{
		public bool Validate(string line, ref int offset, Specification forMeasurement)
		{
			DateTime dateVal;
			bool valid = false;

			if (forMeasurement.Format != null)
			{
				valid = line.Length >= offset + forMeasurement.Format.Length && DateTime.TryParseExact(line.Substring(offset, forMeasurement.Format.Length), forMeasurement.Format, null, System.Globalization.DateTimeStyles.None, out dateVal);
				offset += forMeasurement.Format.Length;
			}

			return valid;
		}
	}

    public class Recipe
    {
		public bool Validate(string recipeFilename, string outputFilename)
		{
            bool allGood = false;

			using (var fl = File.OpenText(recipeFilename))
			{
				try
				{
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(new CamelCaseNamingConvention())
                        .Build();
					var yamlObject = deserializer.Deserialize<Layout[]>(fl);

					using (var flIn = File.OpenText(outputFilename))
					{
						foreach (var layout in yamlObject)
						{
							var nbrOfRows = layout.NumberOfRows;

							do
							{
                                string content = flIn.ReadLine();
								var valid = layout.Validate(content);
								nbrOfRows--;
                                if (content == null && !valid) {
                                    throw new Exception($"Invalid content, expecting {layout.NumberOfRows} rows. Found {layout.NumberOfRows - nbrOfRows - 1} rows");
                                } else if (!valid) {
                                    throw new Exception($"Invalid entry at line: {layout.NumberOfRows - nbrOfRows}, expecting kind of: {layout.Kind}, Name: {layout.Name}");
								}
							}
							while (nbrOfRows > 0);
						}
					}

                    allGood = true;
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
			}

            return allGood;
		}    

        public static T CreateValidator<T>(string strFullyQualifiedName)
		{
			Type t = Type.GetType(strFullyQualifiedName);
            return (T)Activator.CreateInstance(t);
		}
    }

    public class Layout
    {
		public string Kind { get; set; }
        public string Name { get; set; }
		public Specification[] Specs { get; set; }
        public int NumberOfRows { get; set; }
        public KeyValue[] Configs { get; set; }

        public bool Validate(string line)
        {
            try 
            {
                return Recipe.CreateValidator<LayoutValidator>(this.Kind)
                             .Validate(line, this);
			}
            catch (Exception)
            {
                return false;
            }
        }
	}

    public class Specification
    {
        public string Type { get; set; }
        public string Value { get; set; }
        public string Format { get; set; }
        public int Length { get; set; }

        public bool Validate(string line, ref int offset)
        {
			try
			{
                return Recipe.CreateValidator<SpecificationValidator>(this.Type)
                             .Validate(line, ref offset, this);
			}
			catch (Exception)
			{
                return false;
			}
		}
    }

    public class KeyValue
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public static class Extension
    {
        public static string FindValue(this KeyValue[] @this, string key) 
        {
            if (@this == null)
                return null;
            
            foreach(var item in @this)
            {
                if (item.Key.Equals(key, StringComparison.CurrentCultureIgnoreCase))
                    return item.Value;
            }

            return null;
        }
    }
}