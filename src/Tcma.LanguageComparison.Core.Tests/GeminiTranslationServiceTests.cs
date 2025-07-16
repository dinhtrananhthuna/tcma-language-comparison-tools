using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tcma.LanguageComparison.Core.Models;
using Tcma.LanguageComparison.Core.Services;

namespace Tcma.LanguageComparison.Core.Tests
{
    [TestClass]
    public class GeminiTranslationServiceTests
    {
        private const string ValidApiKey = "YOUR_VALID_GEMINI_API_KEY"; // Thay bằng key thật khi test thực tế
        private const string InvalidApiKey = "INVALID_KEY";

        [TestMethod]
        public async Task TranslateBatchAsync_ShouldTranslateSuccessfully()
        {
            var service = new GeminiTranslationService(ValidApiKey);
            var rows = new List<ContentRow>
            {
                new() { ContentId = "1", Content = "Xin chào" },
                new() { ContentId = "2", Content = "Tôi là AI" }
            };
            var result = await service.TranslateBatchAsync(rows, "vi", "en");
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, result.Data!.Count);
            Assert.IsTrue(result.Data.Exists(r => r.ContentId == "1"));
            Assert.IsTrue(result.Data.Exists(r => r.ContentId == "2"));
        }

        [TestMethod]
        public async Task TranslateBatchAsync_ShouldFailWithInvalidApiKey()
        {
            var service = new GeminiTranslationService(InvalidApiKey);
            var rows = new List<ContentRow>
            {
                new() { ContentId = "1", Content = "Xin chào" }
            };
            var result = await service.TranslateBatchAsync(rows, "vi", "en");
            Assert.IsFalse(result.IsSuccess);
        }

        [TestMethod]
        public async Task TranslateBatchAsync_ShouldMapContentIdCorrectly()
        {
            var service = new GeminiTranslationService(ValidApiKey);
            var rows = new List<ContentRow>
            {
                new() { ContentId = "A", Content = "Bonjour" },
                new() { ContentId = "B", Content = "Merci" }
            };
            var result = await service.TranslateBatchAsync(rows, "fr", "en");
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("A", result.Data![0].ContentId);
            Assert.AreEqual("B", result.Data![1].ContentId);
        }

        [TestMethod]
        public async Task TranslateBatchAsync_ShouldHandleLargeBatch()
        {
            var service = new GeminiTranslationService(ValidApiKey);
            var rows = new List<ContentRow>();
            for (int i = 0; i < 20; i++)
            {
                rows.Add(new ContentRow { ContentId = i.ToString(), Content = $"Xin chào {i}" });
            }
            var result = await service.TranslateBatchAsync(rows, "vi", "en");
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(20, result.Data!.Count);
        }
    }
} 