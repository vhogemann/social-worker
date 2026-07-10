# Brand Voice Prompts Plan

We will add Brand Voice Prompts support to let users manage writing personas (e.g. "Professional", "Funny", "Casual") and inject their styles directly into the LLM system prompt.

## 1. Database Schema
- **`BrandVoicePrompt`** entity:
  - `Id`: Guid (Primary Key)
  - `UserId`: Guid (Foreign Key to AppUser)
  - `Name`: string (e.g., "Professional Tone")
  - `Body`: string (guidelines for the model, e.g. "Write concisely, use passive voice...")
  - `IsDefault`: bool (only one prompt can be default per user)
  - `CreatedAt`: DateTime
  - `UpdatedAt`: DateTime

## 2. API Endpoints (`/api/brand-prompts`)
- `GET /api/brand-prompts`: Lists prompts for the authenticated user.
- `GET /api/brand-prompts/{id}`: Retrieves details of a specific prompt.
- `POST /api/brand-prompts`: Creates a new brand prompt. (If set to default, resets other user default flags).
- `PUT /api/brand-prompts/{id}`: Updates a prompt. (Handles default toggles).
- `DELETE /api/brand-prompts/{id}`: Deletes a prompt.

## 3. LLM Injection
- Update `SystemPromptBuilder` to check for the user's default/active brand voice prompt.
- If one exists, append its body to the bottom of the system prompt to instruct the writing style.

## 4. UI / Web
- Add a "Brand Voices" tab in the Admin/Settings Modal.
- Render a list of voices with Edit, Delete, Set Default, and "+ New Brand Voice" actions.
