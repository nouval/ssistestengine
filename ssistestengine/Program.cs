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
            new Recipe().Cook("recipe_csv.yaml", "testfile_csv.txt");
        }
    }

    public interface IngredientValidator
    {
        bool Validate(string line, Ingredient forIngredient);
    }

    public interface MeasurementValidator
    {
        bool Validate(string line, ref int offset, Measurement forMeasurement);
    }

    public class FixedIngredientValidator : IngredientValidator
    {
        public bool Validate(string line, Ingredient forIngredient)
        {
			bool valid = false;
			int offset = 0;

			foreach (var spec in forIngredient.Specs)
			{
				valid = spec.evaluate(line, ref offset);
				if (!valid)
					break;
			}

			return valid;
		}
    }

    public class DelimitedIngredientValidator : IngredientValidator
    {
        public bool Validate(String line, Ingredient forIngredient)
        {
			bool valid = false;
			int offset = 0, index = 0;
			var fields = line.Split(',');

			foreach (var spec in forIngredient.Specs)
			{
                valid = spec.evaluate(fields[index], ref offset);
                offset = 0;
                index++;
				if (!valid)
					break;
			}

			return valid;
		}
    }

    public class StringMeasurementValidator : MeasurementValidator
    {
        public bool Validate(string line, ref int offset, Measurement forMeasurement)
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

	public class DateTimeMeasurementValidator : MeasurementValidator
	{
		public bool Validate(string line, ref int offset, Measurement forMeasurement)
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
		public void Cook(string recipeFilename, string outputFilename)
		{
			using (var fl = File.OpenText(recipeFilename))
			{
				try
				{
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(new CamelCaseNamingConvention())
                        .Build();
					var yamlObject = deserializer.Deserialize<Ingredient[]>(fl);
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
                                    throw new Exception($"Invalid entry at line: {line}, expecting kind of: {ingredient.Kind}, Name: {ingredient.Name}");
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

        public static T CreateTaster<T>(string strFullyQualifiedName)
		{
			Type t = Type.GetType(strFullyQualifiedName);
            return (T)Activator.CreateInstance(t);
		}
    }

    public class Ingredient
    {
		public string Kind { get; set; }
        public string Name { get; set; }
		public Measurement[] Specs { get; set; }
        public int NumberOfRows { get; set; }

        public bool evaluate(string line)
        {
            try 
            {
                return Recipe.CreateTaster<IngredientValidator>(this.Kind)
                             .Validate(line, this);
			}
            catch (Exception)
            {
                throw new Exception("Wrong 'Kind' you dummy :)");
            }
        }
	}

    public class Measurement
    {
        public string Type { get; set; }
        public string Value { get; set; }
        public string Format { get; set; }
        public int Length { get; set; }

        public bool evaluate(string line, ref int offset)
        {
			try
			{
                return Recipe.CreateTaster<MeasurementValidator>(this.Type)
                             .Validate(line, ref offset, this);
			}
			catch (Exception)
			{
				throw new Exception("Wrong 'Type' you dummy :)");
			}
		}
    }
}