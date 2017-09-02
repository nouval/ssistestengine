using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ssistestengine
{
    public class MainClass
    {
        public static void Main(string[] args)
        {
            new Recipe().Cook("recipe.yaml", "testfile.txt");
        }
    }

    public class Recipe
    {
		public void Cook(string recipeFilename, string outputFilename)
		{
			using (var fl = File.OpenText(recipeFilename))
			{
				try
				{
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(new CamelCaseNamingConvention())
                        .Build();
					var yamlObject = deserializer.Deserialize<Ingredients[]>(fl);
					var line = 0;

					using (var flIn = File.OpenText(outputFilename))
					{
						foreach (var ingredient in yamlObject)
						{
							var nbrOfRows = ingredient.NumberOfRows;

							do
							{
								var valid = ingredient.evaluate(flIn.ReadLine());
								nbrOfRows--;
								line++;
								if (!valid)
								{
									throw new Exception($"Invalid entry at line: {line}, expecting kind of: {ingredient.Kind}");
								}
							}
							while (nbrOfRows > 0);
						}
					}
					Console.WriteLine("All Good");
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
				}
			}
		}        
    }

    public class Ingredients
    {
        const string HeaderKind = "header";
        const string DetailKind = "detail";
        const string FooterKind = "footer";

		public string Kind { get; set; }

		public Specification[] Specs { get; set; }

        public int NumberOfRows { get; set; }

        public string Delimiter { get; set; }

        public bool evaluate(string line) 
        {
            switch (this.Kind.ToLower()) 
            {
                case Ingredients.HeaderKind:
                case Ingredients.DetailKind:
                case Ingredients.FooterKind:
                    return evaluateAnyKind(line);
                default:
                    throw new Exception("Wrong 'Kind' you dummy :)");
            }
        }

		private bool evaluateAnyKind(string line)
		{
            bool valid = false;
            int offset = 0;

            foreach(var spec in this.Specs)
            {
                valid = spec.evaluate(line, ref offset);
                if (!valid)
                    break;
            }

            return valid;
		}
	}

    public class Specification
    {
        const string StringType = "string";
        const string DateTimeType = "datetime";

        public string Type { get; set; }

        public string Value { get; set; }

        public string Format { get; set; }

        public int Length { get; set; }

        public bool evaluate(string line, ref int offset)
        {
            switch (this.Type.ToLower())
			{
				case Specification.StringType:
                    return evaluateStringType(line, ref offset);
                case Specification.DateTimeType:
					return evaluateDateTimeType(line, ref offset);
				default:
					throw new Exception("Wrong 'Type' you dummy :)");
			}
		}

        private bool evaluateStringType(string line, ref int offset)
        {
            bool valid = false;

			if (this.Value != null)
            {
                valid = line.Length >= offset + this.Value.Length && line.Substring(offset, this.Value.Length).Equals(this.Value);
                offset += this.Value.Length;
            }
            else if (this.Length != 0)
            {
                valid = line.Length >= offset + this.Length && line.Substring(offset, this.Length) != null;
				offset += this.Length;

			}

            return valid;
        }

        private bool evaluateDateTimeType(string line, ref int offset)
        {
			DateTime dateVal;
            bool valid = false;

            if (this.Format != null) 
            {
                valid = line.Length >= offset + this.Format.Length && DateTime.TryParseExact(line.Substring(offset, this.Format.Length), this.Format, null, System.Globalization.DateTimeStyles.None, out dateVal);
                offset += this.Format.Length;
            }

            return valid;
        }
    }
}