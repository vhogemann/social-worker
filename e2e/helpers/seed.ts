import { apiFetchAsUser } from "./api.js";

export interface SeedProvider {
  id: string;
}

export interface SeedAccount {
  id: string;
}

export interface SeedDraft {
  id: string;
}

export async function seedProvider(token: string): Promise<SeedProvider> {
  const res = await apiFetchAsUser("/api/providers", token, {
    method: "POST",
    body: JSON.stringify({
      name: `Demo Provider ${Date.now()}`,
      providerType: "OpenRouter",
      baseUrl: "https://demo.local/api/v1",
      model: "demo-model",
      isDefault: true,
    }),
  });
  if (!res.ok) {
    // If name conflict, try with a random suffix
    if (res.status === 409) {
      const retry = await apiFetchAsUser("/api/providers", token, {
        method: "POST",
        body: JSON.stringify({
          name: `Demo Provider ${crypto.randomUUID()}`,
          providerType: "OpenRouter",
          baseUrl: "https://demo.local/api/v1",
          model: "demo-model",
          isDefault: true,
        }),
      });
      if (!retry.ok) throw new Error(`seedProvider failed: ${retry.status}`);
      return retry.json();
    }
    throw new Error(`seedProvider failed: ${res.status}`);
  }
  return res.json();
}

export async function seedBrandVoice(token: string): Promise<void> {
  const res = await apiFetchAsUser("/api/brand-prompts", token, {
    method: "POST",
    body: JSON.stringify({
      name: "Professional Tone",
      body: "Write in a clear, professional tone. Use industry-appropriate terminology. Be concise and authoritative.",
      isDefault: true,
    }),
  });
  if (!res.ok) throw new Error(`seedBrandVoice failed: ${res.status}`);
}

export async function seedAccount(token: string): Promise<SeedAccount> {
  const res = await apiFetchAsUser("/api/accounts", token, {
    method: "POST",
    body: JSON.stringify({
      platform: "Bluesky",
      handle: "demo.bsky.social",
      appPassword: "demo-app-password",
    }),
  });
  if (!res.ok) throw new Error(`seedAccount failed: ${res.status}`);
  return { id: "seeded-account" };
}

export async function seedDraft(token: string, title?: string, content?: string): Promise<SeedDraft> {
  const res = await apiFetchAsUser("/api/drafts", token, {
    method: "POST",
    body: JSON.stringify({
      title: title ?? "5 Tips for Better Social Media Engagement",
      content:
        content ??
        "Want to boost your social media engagement? Here are five tips that actually work.\n\n---\n\n**1. Know your audience**\nUnderstand who you're talking to. Research their interests, pain points, and preferred content formats.\n\n---\n\n**2. Post consistently**\nRegular posting builds trust. Create a content calendar and stick to it.\n\n---\n\n**3. Use visuals**\nPosts with images get 2.3x more engagement. Invest in quality visuals.\n\n---\n\n**4. Engage back**\nSocial media is a two-way street. Reply to comments, ask questions, and join conversations.\n\n---\n\n**5. Analyze and adapt**\nTrack what works. Use analytics to refine your strategy over time.",
      targetPlatform: "Bluesky",
    }),
  });
  if (!res.ok) throw new Error(`seedDraft failed: ${res.status}`);
  return res.json();
}

export async function seedAll(token: string): Promise<{ provider: SeedProvider; account: SeedAccount; draft: SeedDraft }> {
  const provider = await seedProvider(token);
  await seedBrandVoice(token);
  const account = await seedAccount(token);
  const draft = await seedDraft(token);
  return { provider, account, draft };
}