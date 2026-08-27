import React from "react";

export function PrivacyPolicyPage() {
  return (
    <div className="h-full overflow-y-auto bg-zinc-50 dark:bg-zinc-950 text-zinc-800 dark:text-zinc-200 font-sans py-12 px-6" data-testid="privacy-policy-page">
      <div className="max-w-2xl mx-auto bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-2xl p-8 shadow-sm">
        <h1 className="text-3xl font-extrabold tracking-tight text-zinc-900 dark:text-white mb-2">
          Privacy Policy
        </h1>
        <p className="text-xs text-zinc-500 dark:text-zinc-400 font-mono mb-6">
          Last Updated: August 10, 2026
        </p>

        <section className="space-y-6 text-sm leading-relaxed">
          <p>
            This Privacy Policy describes how <strong>social-worker</strong> (the "Application") handles information. 
            The Application is a local-first, self-hosted tool designed to help you compose and publish content to your social media accounts.
          </p>

          <div>
            <h2 className="text-lg font-bold text-zinc-900 dark:text-white mb-2">
              1. Information We Collect
            </h2>
            <p className="mb-2">
              Because the Application runs completely locally in your own Docker environment, we do not run centralized databases or tracking servers. 
            </p>
            <ul className="list-disc list-inside space-y-1 pl-2">
              <li>
                <strong>Credentials:</strong> Any API keys, App Passwords, or OAuth access tokens you provide (e.g. for Bluesky or LinkedIn) are stored locally in your database container and are encrypted using an AES key that you specify in your local environment variables.
              </li>
              <li>
                <strong>Draft Content:</strong> Your drafts, uploaded media assets, and search logs are stored strictly inside your self-hosted instance.
              </li>
            </ul>
          </div>

          <div>
            <h2 className="text-lg font-bold text-zinc-900 dark:text-white mb-2">
              2. Third-Party Services
            </h2>
            <p>
              When you choose to publish a post or thread, the Application transmits the content directly to the third-party platforms you authorize (e.g., Bluesky, LinkedIn) via their official APIs. The handling of that data is governed by the privacy policies of those respective platforms.
            </p>
          </div>

          <div>
            <h2 className="text-lg font-bold text-zinc-900 dark:text-white mb-2">
              3. Security
            </h2>
            <p>
              Your credentials are encrypted at rest using standard AES encryption. We recommend securing the host system running your Docker containers to prevent unauthorized local access to your database.
            </p>
          </div>

          <div>
            <h2 className="text-lg font-bold text-zinc-900 dark:text-white mb-2">
              4. Changes to This Policy
            </h2>
            <p>
              As a self-hosted open-source codebase, any updates to this privacy policy will be made transparently in the repository. Your continued use of the application constitutes acceptance of any updates.
            </p>
          </div>
        </section>
      </div>
    </div>
  );
}

export { PrivacyPolicyPage as PrivacyPolicy };
