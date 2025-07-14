# Project Plan: Language Content Comparison Tool

This document outlines the development plan for a C# Windows tool designed to compare and align localization content from two CSV files using AI.

## 1. Problem Statement

Localization files (e.g., English and Korean) exported from a system may have misaligned rows due to translation order discrepancies or missing lines. The `ContentId` for the same content element can also differ between files.

The goal is to build a tool that can:
1.  Intelligently match corresponding content lines between a reference language file (e.g., English) and another language file.
2.  Identify discrepancies (mismatched or missing lines).
3.  Provide an option to automatically reorder the second file to match the reference file's structure.

## 2. Core Technology

The core of the solution will be based on **Text Embeddings**.

-   **How it works:** Each content string (after cleaning HTML tags) will be converted into a numerical vector (an "embedding") that represents its semantic meaning.
-   **Comparison:** By calculating the cosine similarity between vectors from the two files, we can find pairs of content with the most similar meanings, regardless of the language.
-   **Chosen Model:** We will use the **Google Gemini Embedding API** (e.g., `text-embedding-004` model) for its high quality and multilingual capabilities. The user will provide their own API key.

---

## 3. Development Roadmap

### Phase 1: Core Logic & Proof of Concept (Console App)

**Goal:** Validate the embedding-based matching algorithm.

-   [ ] **1.1. Project Setup:**
    -   Create a new C# .NET Console Application project.
    -   Add necessary NuGet packages:
        -   `CsvHelper`: For robust CSV file parsing.
        -   `Google.Apis.GenerativeLanguage.v1beta`: To interact with the Gemini API.
-   [ ] **1.2. Data Handling:**
    -   Implement a class/record to represent a CSV row (`ContentId`, `Content`).
    -   Create a CSV reader utility using `CsvHelper` to load the two files into lists of objects.
-   [ ] **1.3. Pre-processing:**
    -   Create a utility function to strip HTML tags from the `Content` string to get clean text for the AI model. (e.g., using Regex or a dedicated library).
-   [ ] **1.4. Gemini API Integration:**
    -   Create a service class to handle communication with the Gemini API.
    -   Implement a function `GetEmbeddingAsync(string text, string apiKey)` that takes a text string and returns its embedding vector (a `float[]`).
    -   Handle API authentication using the user-provided key.
-   [ ] **1.5. Matching Algorithm:**
    -   For each row in the reference (English) file:
        -   Generate its embedding vector.
        -   Iterate through all rows in the other language file.
        -   Generate an embedding vector for each.
        -   Calculate the cosine similarity between the English vector and each of the other language's vectors.
        -   Identify the row with the highest similarity score as the best match.
-   [ ] **1.6. Basic Output:**
    -   Print the matched pairs and their similarity scores to the console.
    -   Identify and report any rows from the reference file that don't have a strong match in the other file.

### Phase 2: Windows GUI Application (WPF or Windows Forms)

**Goal:** Create a user-friendly graphical interface.

-   [ ] **2.1. UI/UX Design:**
    -   Create a new WPF or Windows Forms project.
    -   Design a simple interface with:
        -   Two "Upload File" buttons (for reference and target languages).
        -   A text box for the user to input their Gemini API Key.
        -   A "Compare" button.
        -   A `DataGrid` or similar component to display results.
        -   A "Export Corrected File" button.
-   [ ] **2.2. UI Integration:**
    -   Connect UI controls to the core logic developed in Phase 1.
    -   Implement file dialogs for selecting CSV files.
-   [ ] **2.3. Results Display:**
    -   Populate the `DataGrid` with the comparison results.
    -   Columns: `Ref. Line #`, `Ref. Content`, `Matched Content`, `Similarity Score`.
    -   Use color-coding to indicate match quality (e.g., green for high similarity, yellow for medium, red for low/no match).

### Phase 3: File Correction and Export

**Goal:** Implement the final, core feature of the tool.

-   [ ] **3.1. Data Reordering:**
    -   Based on the mapping results from the comparison, create a new, reordered list of the target language's content rows. The new order should align with the reference file's row order.
-   [ ] **3.2. Export to CSV:**
    -   When the "Export" button is clicked, use `CsvHelper` to write the reordered list to a new CSV file. This new file will have the original `ContentId` and `Content` but in the corrected order.

### Phase 4: Enhancements & Polish

**Goal:** Improve usability and robustness.

-   [ ] **4.1. Configurable Threshold:**
    -   Add a setting (e.g., a slider or numeric input) that allows the user to define the minimum similarity score required to be considered a "good match".
-   [ ] **4.2. Error Handling:**
    -   Implement robust error handling for file I/O errors, invalid CSV formats, and API call failures (e.g., invalid API key, network issues).
-   [ ] **4.3. User Feedback:**
    -   Display clear status messages to the user (e.g., "Processing...", "Comparison Complete", "Error: ...").
-   [ ] **4.4. Manual Override (Optional):**
    -   (Advanced) Add functionality for the user to manually correct a match proposed by the AI. 