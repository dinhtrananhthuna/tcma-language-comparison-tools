#!/usr/bin/env python3
"""
Test script để kiểm tra thuật toán alignment
So sánh file output với file reference dựa trên ContentId
"""

import subprocess
import csv
import json
import os
import sys
from pathlib import Path

def read_csv_file(file_path):
    """Đọc file CSV và trả về dict với ContentId làm key"""
    content_dict = {}
    with open(file_path, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            content_id = row.get('ContentId', '').strip()
            content = row.get('Content', '').strip()
            if content_id and content:
                content_dict[content_id] = content
    return content_dict

def create_test_appsettings():
    """Tạo file appsettings test với API key"""
    api_key = input("Nhập Gemini API Key: ").strip()
    if not api_key:
        print("❌ Cần API key để test!")
        sys.exit(1)
    
    # Tạo appsettings cho test
    test_settings = {
        "LanguageComparison": {
            "SimilarityThreshold": 0.35,
            "MaxEmbeddingBatchSize": 50,
            "MaxConcurrentRequests": 3,
            "MinContentLength": 3,
            "MaxContentLength": 8000,
            "DemoRowLimit": 0
        },
        "Output": {
            "ShowDetailedResults": True,
            "ShowProgressMessages": True,
            "ExportUnmatchedAsPlaceholder": True
        },
        "Preprocessing": {
            "StripHtmlTags": True,
            "NormalizeWhitespace": True,
            "RemoveSpecialCharacters": True
        },
        "GeminiApiKey": api_key
    }
    
    with open('src/Tcma.LanguageComparison.Core/appsettings.test.json', 'w') as f:
        json.dump(test_settings, f, indent=2)
    
    return api_key

def run_alignment_test():
    """Chạy test alignment với 2 file sample"""
    
    print("🧪 Bắt đầu test thuật toán alignment...")
    
    # Đường dẫn file
    ref_file = "sample/NX Essentials Google Ads Page_20250529-EN.csv"
    target_file = "sample/NX Essentials Google Ads Page_20250529-DE.csv"
    output_file = "test_output_aligned.csv"
    
    # Kiểm tra file tồn tại
    if not os.path.exists(ref_file):
        print(f"❌ Không tìm thấy file reference: {ref_file}")
        return False
        
    if not os.path.exists(target_file):
        print(f"❌ Không tìm thấy file target: {target_file}")
        return False
    
    print(f"📂 Reference file: {ref_file}")
    print(f"📂 Target file: {target_file}")
    
    # Đọc file gốc để có baseline
    print("\n📊 Đọc dữ liệu gốc...")
    ref_data = read_csv_file(ref_file)
    target_data = read_csv_file(target_file)
    
    print(f"   Reference: {len(ref_data)} items")
    print(f"   Target: {len(target_data)} items")
    
    # Build và chạy core app
    print("\n🔨 Building application...")
    build_result = subprocess.run(
        ["dotnet", "build"], 
        cwd=".",
        capture_output=True, 
        text=True
    )
    
    if build_result.returncode != 0:
        print(f"❌ Build failed: {build_result.stderr}")
        return False
    
    print("✅ Build successful!")
    
    # Chạy comparison
    print("\n🚀 Running alignment test...")
    
    # Tạo script C# để chạy comparison
    create_csharp_test_runner(ref_file, target_file, output_file)
    
    # Chạy test
    test_result = subprocess.run(
        ["dotnet", "run", "--project", "src/Tcma.LanguageComparison.Core", "--", "test"],
        cwd=".",
        capture_output=True,
        text=True
    )
    
    if test_result.returncode != 0:
        print(f"❌ Test failed: {test_result.stderr}")
        return False
    
    print("✅ Test completed!")
    
    # Phân tích kết quả
    return analyze_alignment_results(ref_data, output_file)

def create_csharp_test_runner(ref_file, target_file, output_file):
    """Tạo C# test runner để chạy alignment"""
    
    test_code = f'''
using System;
using System.IO;
using System.Threading.Tasks;
using Tcma.LanguageComparison.Core.Services;
using Tcma.LanguageComparison.Core.Models;

namespace Tcma.LanguageComparison.Core
{{
    public class AlignmentTester
    {{
        public static async Task RunTestAsync()
        {{
            try
            {{
                Console.WriteLine("🧪 Starting alignment test...");
                
                // Services
                var csvService = new CsvReaderService();
                var geminiService = new GeminiEmbeddingService("YOUR_API_KEY_HERE");
                var preprocessingService = new TextPreprocessingService();
                var matchingService = new ContentMatchingService(0.35);
                
                // Load files
                Console.WriteLine("📂 Loading reference file...");
                var refResult = await csvService.ReadContentRowsAsync("{ref_file}");
                if (!refResult.IsSuccess) throw new Exception(refResult.Error?.UserMessage);
                var referenceRows = refResult.Data!;
                
                Console.WriteLine("📂 Loading target file...");
                var targetResult = await csvService.ReadContentRowsAsync("{target_file}");
                if (!targetResult.IsSuccess) throw new Exception(targetResult.Error?.UserMessage);
                var targetRows = targetResult.Data!;
                
                // Preprocessing
                Console.WriteLine("🔧 Preprocessing content...");
                preprocessingService.ProcessContentRows(referenceRows);
                preprocessingService.ProcessContentRows(targetRows);
                
                // Generate embeddings
                Console.WriteLine("🧠 Generating embeddings for reference...");
                var refEmbResult = await geminiService.GenerateEmbeddingsAsync(referenceRows, null);
                if (!refEmbResult.IsSuccess) throw new Exception(refEmbResult.Error?.UserMessage);
                
                Console.WriteLine("🧠 Generating embeddings for target...");
                var targetEmbResult = await geminiService.GenerateEmbeddingsAsync(targetRows, null);
                if (!targetEmbResult.IsSuccess) throw new Exception(targetEmbResult.Error?.UserMessage);
                
                // Generate aligned display data
                Console.WriteLine("⚡ Running alignment algorithm...");
                var alignedData = await matchingService.GenerateAlignedDisplayDataAsync(referenceRows, targetRows, null);
                
                // Export result
                Console.WriteLine("💾 Exporting aligned result...");
                var exportResult = await csvService.ExportAlignedDisplayRowsAsync("{output_file}", alignedData);
                if (!exportResult.IsSuccess) throw new Exception(exportResult.Error?.UserMessage);
                
                Console.WriteLine("✅ Test completed successfully!");
                Console.WriteLine($"📁 Output file: {output_file}");
                Console.WriteLine($"📊 Aligned {alignedData.Count} rows");
            }}
            catch (Exception ex)
            {{
                Console.WriteLine($"❌ Test failed: {{ex.Message}}");
                Environment.Exit(1);
            }}
        }}
    }}
}}
'''
    
    # Tạo file test runner
    with open('src/Tcma.LanguageComparison.Core/AlignmentTester.cs', 'w') as f:
        f.write(test_code)

def analyze_alignment_results(ref_data, output_file):
    """Phân tích kết quả alignment"""
    
    print(f"\n📊 Analyzing alignment results from {output_file}...")
    
    if not os.path.exists(output_file):
        print(f"❌ Output file not found: {output_file}")
        return False
    
    # Đọc output file
    output_data = read_csv_file(output_file)
    
    print(f"   Output contains: {len(output_data)} items")
    
    # So sánh alignment
    correct_alignments = 0
    total_comparisons = 0
    misalignments = []
    
    for content_id, ref_content in ref_data.items():
        total_comparisons += 1
        
        if content_id in output_data:
            output_content = output_data[content_id]
            if content_id in output_content or ref_content == output_content:
                correct_alignments += 1
            else:
                misalignments.append({
                    'content_id': content_id,
                    'ref_content': ref_content[:50] + "...",
                    'output_content': output_content[:50] + "..."
                })
        else:
            misalignments.append({
                'content_id': content_id,
                'ref_content': ref_content[:50] + "...",
                'output_content': "MISSING"
            })
    
    # Kết quả
    accuracy = (correct_alignments / total_comparisons) * 100 if total_comparisons > 0 else 0
    
    print(f"\n📈 Alignment Test Results:")
    print(f"   ✅ Correct alignments: {correct_alignments}/{total_comparisons}")
    print(f"   📊 Accuracy: {accuracy:.2f}%")
    
    if misalignments:
        print(f"\n❌ Found {len(misalignments)} misalignments:")
        for i, mis in enumerate(misalignments[:5]):  # Show first 5
            print(f"   {i+1}. ID: {mis['content_id']}")
            print(f"      Ref: {mis['ref_content']}")
            print(f"      Out: {mis['output_content']}")
        
        if len(misalignments) > 5:
            print(f"   ... and {len(misalignments) - 5} more misalignments")
    
    return accuracy >= 80  # Consider 80%+ as success

def main():
    """Main test function"""
    print("🧪 TCMA Language Comparison - Alignment Test")
    print("=" * 50)
    
    try:
        # Tạo test settings
        api_key = create_test_appsettings()
        
        # Chạy test
        success = run_alignment_test()
        
        if success:
            print("\n🎉 Test PASSED! Thuật toán alignment hoạt động đúng.")
        else:
            print("\n❌ Test FAILED! Cần kiểm tra lại thuật toán alignment.")
            
    except KeyboardInterrupt:
        print("\n⏹️ Test interrupted by user.")
    except Exception as e:
        print(f"\n💥 Unexpected error: {e}")
    finally:
        # Cleanup
        cleanup_files = [
            'src/Tcma.LanguageComparison.Core/appsettings.test.json',
            'src/Tcma.LanguageComparison.Core/AlignmentTester.cs',
            'test_output_aligned.csv'
        ]
        for file in cleanup_files:
            if os.path.exists(file):
                os.remove(file)

if __name__ == "__main__":
    main() 