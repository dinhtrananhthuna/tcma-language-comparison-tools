using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mscc.GenerativeAI;
using DotNetEnv;

namespace Tcma.LanguageComparison.Core
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== TCMA Language Comparison Tools ===");
            Console.WriteLine("Testing Gemini API connection and embedding functionality...\n");

            // Load environment variables from .env file
            Env.Load();

            // API Key configuration
            var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("ERROR: GEMINI_API_KEY environment variable is not set.");
                Console.WriteLine("Please set your Gemini API key as an environment variable:");
                Console.WriteLine("set GEMINI_API_KEY=your_api_key_here");
                Console.WriteLine("Or create a .env file with: GEMINI_API_KEY=your_api_key_here");
                return;
            }

            try
            {
                // Initialize Google AI client
                var googleAI = new GoogleAI(apiKey);
                var model = googleAI.GenerativeModel(Model.TextEmbedding004);

                Console.WriteLine("✓ Successfully initialized Gemini client");

                // Test texts for embedding comparison
                var text1 = "Hello, how are you today?";
                var text2 = "Hi, how are you doing?";
                var text3 = "The weather is beautiful today.";

                Console.WriteLine($"\nTesting embedding generation for:");
                Console.WriteLine($"Text 1: \"{text1}\"");
                Console.WriteLine($"Text 2: \"{text2}\"");
                Console.WriteLine($"Text 3: \"{text3}\"");

                // Generate embeddings
                Console.WriteLine("\nGenerating embeddings...");
                
                var embedding1 = await model.EmbedContent(text1);
                var embedding2 = await model.EmbedContent(text2);
                var embedding3 = await model.EmbedContent(text3);

                Console.WriteLine("✓ Successfully generated embeddings");

                // Extract embedding values
                var values1 = embedding1.Embedding?.Values ?? new List<float>();
                var values2 = embedding2.Embedding?.Values ?? new List<float>();
                var values3 = embedding3.Embedding?.Values ?? new List<float>();

                Console.WriteLine($"Embedding dimension: {values1.Count}");

                // Calculate cosine similarities
                var similarity12 = CalculateCosineSimilarity(values1, values2);
                var similarity13 = CalculateCosineSimilarity(values1, values3);
                var similarity23 = CalculateCosineSimilarity(values2, values3);

                Console.WriteLine("\n=== Similarity Results ===");
                Console.WriteLine($"Similarity between Text 1 & Text 2: {similarity12:F4}");
                Console.WriteLine($"Similarity between Text 1 & Text 3: {similarity13:F4}");
                Console.WriteLine($"Similarity between Text 2 & Text 3: {similarity23:F4}");

                Console.WriteLine("\n=== Analysis ===");
                Console.WriteLine("Text 1 and Text 2 should have higher similarity (both are greetings)");
                Console.WriteLine("Text 3 should have lower similarity with Text 1 and 2 (different topic)");

                if (similarity12 > similarity13 && similarity12 > similarity23)
                {
                    Console.WriteLine("✓ Results look correct - similar greetings have highest similarity!");
                }
                else
                {
                    Console.WriteLine("⚠ Unexpected results - similarity patterns may need review.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Calculate cosine similarity between two embedding vectors
        /// </summary>
        /// <param name="vector1">First embedding vector</param>
        /// <param name="vector2">Second embedding vector</param>
        /// <returns>Cosine similarity value between -1 and 1</returns>
        static double CalculateCosineSimilarity(IList<float> vector1, IList<float> vector2)
        {
            if (vector1.Count != vector2.Count)
            {
                throw new ArgumentException("Vectors must have the same length");
            }

            double dotProduct = 0.0;
            double magnitude1 = 0.0;
            double magnitude2 = 0.0;

            for (int i = 0; i < vector1.Count; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                magnitude1 += vector1[i] * vector1[i];
                magnitude2 += vector2[i] * vector2[i];
            }

            magnitude1 = Math.Sqrt(magnitude1);
            magnitude2 = Math.Sqrt(magnitude2);

            if (magnitude1 == 0.0 || magnitude2 == 0.0)
            {
                return 0.0;
            }

            return dotProduct / (magnitude1 * magnitude2);
        }
    }
}
