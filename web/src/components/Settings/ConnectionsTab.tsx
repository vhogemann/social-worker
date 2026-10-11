import { useState, useEffect } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlus, faTrashCan, faShareNodes, faRotateRight } from "@fortawesome/free-solid-svg-icons";
import { type AccountDto, listAccounts, saveAccount, deleteAccount, getLinkedInAuthUrl, getMetaThreadsAuthUrl, getTwitterAuthUrl, getInstagramAuthUrl } from "../../api/accounts";
import { PLATFORM_ICONS, getPlatformLabel } from "../../config/platformBadges";
import { Modal } from "../common/Modal";
import { SettingsSection } from "./common/SettingsSection";
import { SettingsEmptyState } from "./common/SettingsEmptyState";
import { SettingsAlert } from "./common/SettingsAlert";

export function ConnectionsTab() {
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [modalError, setModalError] = useState<string | null>(null);
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);

  const [platform, setPlatform] = useState("Bluesky");
  const [handle, setHandle] = useState("");
  const [appPassword, setAppPassword] = useState("");
  const [linkedInType, setLinkedInType] = useState<"personal" | "company">("personal");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const fetchAccounts = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await listAccounts();
      setAccounts(data);
    } catch (err: any) {
      setError(err.message || "Failed to load connections");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchAccounts();
  }, []);

  const handleOpenAddModal = () => {
    setPlatform("Bluesky");
    setHandle("");
    setAppPassword("");
    setLinkedInType("personal");
    setModalError(null);
    setIsAddModalOpen(true);
  };

  const handleCloseAddModal = () => {
    if (isSubmitting) return;
    setIsAddModalOpen(false);
    setModalError(null);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!platform || !handle || !appPassword) return;

    try {
      setIsSubmitting(true);
      setModalError(null);
      await saveAccount({ platform, handle, appPassword });
      setHandle("");
      setAppPassword("");
      setIsAddModalOpen(false);
      await fetchAccounts();
    } catch (err: any) {
      setModalError(err.message || "Failed to save connection");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleLinkedInConnect = async (type: "personal" | "company" = linkedInType) => {
    try {
      setIsSubmitting(true);
      setModalError(null);
      const data = await getLinkedInAuthUrl(type);
      if (data.authUrl) {
        window.location.href = data.authUrl;
      }
    } catch (err: any) {
      setModalError(err.message || "Failed to initiate LinkedIn connection.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleMetaThreadsConnect = async () => {
    try {
      setIsSubmitting(true);
      setModalError(null);
      const data = await getMetaThreadsAuthUrl();
      if (data.authUrl) {
        window.location.href = data.authUrl;
      }
    } catch (err: any) {
      setModalError(err.message || "Failed to initiate Threads connection.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleTwitterConnect = async () => {
    try {
      setIsSubmitting(true);
      setModalError(null);
      const data = await getTwitterAuthUrl();
      if (data.authUrl) {
        window.location.href = data.authUrl;
      }
    } catch (err: any) {
      setModalError(err.message || "Failed to initiate Twitter / X connection.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleInstagramConnect = async () => {
    try {
      setIsSubmitting(true);
      setModalError(null);
      const data = await getInstagramAuthUrl();
      if (data.authUrl) {
        window.location.href = data.authUrl;
      }
    } catch (err: any) {
      setModalError(err.message || "Failed to initiate Instagram connection.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm("Are you sure you want to remove this connection?")) return;
    try {
      await deleteAccount(id);
      await fetchAccounts();
    } catch (err: any) {
      setError(err.message || "Failed to delete connection");
    }
  };

  return (
    <div className="space-y-6 font-sans">
      {error && <SettingsAlert type="error" message={error} onDismiss={() => setError(null)} />}

      <SettingsSection
        title="Your Connections"
        description="Connect and manage your social platform accounts for direct publishing."
        action={
          <button
            type="button"
            onClick={handleOpenAddModal}
            className="inline-flex items-center gap-1.5 bg-accent text-bg font-medium text-xs py-1.5 px-3.5 rounded hover:opacity-90 transition-opacity font-mono uppercase tracking-wider shadow-sm"
          >
            <FontAwesomeIcon icon={faPlus} className="w-3 h-3" />
            <span>Add Connection</span>
          </button>
        }
      >
        {loading ? (
          <div className="text-xs text-muted font-mono py-6 text-center">Loading connections...</div>
        ) : accounts.length === 0 ? (
          <SettingsEmptyState
            icon={faShareNodes}
            title="No connections configured yet"
            description="Add your Bluesky, LinkedIn, or Threads accounts to start publishing threads directly from social-worker."
            actionLabel="+ Add Connection"
            onAction={handleOpenAddModal}
          />
        ) : (
          <div className="space-y-2.5">
            {accounts.map((acc) => {
              const icon = PLATFORM_ICONS[acc.platform];
              return (
                <div
                  key={acc.id}
                  className="flex items-center justify-between p-3.5 border border-border bg-bg/20 rounded-lg hover:border-border/80 transition-colors"
                >
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-lg bg-border/20 border border-border flex items-center justify-center text-accent">
                      {icon ? (
                        <FontAwesomeIcon icon={icon} className="w-4 h-4" />
                      ) : (
                        <FontAwesomeIcon icon={faShareNodes} className="w-3.5 h-3.5" />
                      )}
                    </div>
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-medium text-foreground">{getPlatformLabel(acc.platform)}</span>
                        {acc.platform === "LinkedIn" && (
                          <span className={`text-[10px] font-mono uppercase tracking-wider px-1.5 py-0.5 rounded border ${
                            acc.handle.toLowerCase().includes("organization")
                              ? "border-indigo-800/40 bg-indigo-950/30 text-indigo-400"
                              : "border-blue-800/40 bg-blue-950/30 text-blue-400"
                          }`}>
                            {acc.handle.toLowerCase().includes("organization") ? "Company Page" : "Personal"}
                          </span>
                        )}
                        {acc.tokenExpiresAt && (
                          <span className="text-[10px] font-mono uppercase tracking-wider px-1.5 py-0.5 rounded border border-zinc-700 bg-zinc-800/60 text-zinc-300">
                            Expires {new Date(acc.tokenExpiresAt).toLocaleDateString()}
                          </span>
                        )}
                        <span className="text-[10px] font-mono uppercase tracking-wider px-1.5 py-0.5 rounded border border-green-800/40 bg-green-950/30 text-green-400">
                          {acc.status}
                        </span>
                      </div>
                      <div className="text-xs text-muted font-mono mt-0.5">
                        @{acc.handle.replace(/^@/, "")}
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    {acc.platform === "MetaThreads" && (
                      <button
                        onClick={handleMetaThreadsConnect}
                        disabled={isSubmitting}
                        className="text-xs font-mono uppercase tracking-wider text-accent hover:text-accent/80 px-2.5 py-1 border border-accent/30 bg-accent/10 hover:bg-accent/20 rounded transition-colors inline-flex items-center gap-1.5 disabled:opacity-50"
                        title="Re-authenticate Threads account"
                      >
                        <FontAwesomeIcon icon={faRotateRight} className={`w-3 h-3 ${isSubmitting ? "animate-spin" : ""}`} />
                        <span>Reconnect</span>
                      </button>
                    )}
                    {acc.platform === "Twitter" && (
                      <button
                        onClick={handleTwitterConnect}
                        disabled={isSubmitting}
                        className="text-xs font-mono uppercase tracking-wider text-accent hover:text-accent/80 px-2.5 py-1 border border-accent/30 bg-accent/10 hover:bg-accent/20 rounded transition-colors inline-flex items-center gap-1.5 disabled:opacity-50"
                        title="Re-authenticate Twitter / X account"
                      >
                        <FontAwesomeIcon icon={faRotateRight} className={`w-3 h-3 ${isSubmitting ? "animate-spin" : ""}`} />
                        <span>Reconnect</span>
                      </button>
                    )}
                    {acc.platform === "LinkedIn" && (
                      <button
                        onClick={() => {
                          setPlatform("LinkedIn");
                          setIsAddModalOpen(true);
                        }}
                        disabled={isSubmitting}
                        className="text-xs font-mono uppercase tracking-wider text-accent hover:text-accent/80 px-2.5 py-1 border border-accent/30 bg-accent/10 hover:bg-accent/20 rounded transition-colors inline-flex items-center gap-1.5 disabled:opacity-50"
                        title="Re-authenticate LinkedIn account"
                      >
                        <FontAwesomeIcon icon={faRotateRight} className="w-3 h-3" />
                        <span>Reconnect</span>
                      </button>
                    )}
                    <button
                      onClick={() => handleDelete(acc.id)}
                      className="text-xs font-mono uppercase tracking-wider text-red-400 hover:text-red-300 px-2.5 py-1 border border-red-900/30 bg-red-950/20 hover:bg-red-950/40 rounded transition-colors inline-flex items-center gap-1.5"
                    >
                      <FontAwesomeIcon icon={faTrashCan} className="w-3 h-3" />
                      <span>Remove</span>
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </SettingsSection>

      {/* Add Connection Modal */}
      <Modal
        isOpen={isAddModalOpen}
        onClose={handleCloseAddModal}
        title="Add Social Connection"
        description="Link a new social platform account to enable direct publishing."
        maxWidth="md"
      >
        {modalError && <SettingsAlert type="error" message={modalError} onDismiss={() => setModalError(null)} />}

        <div className="space-y-4">
          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              Platform
            </label>
            <select
              value={platform}
              onChange={(e) => {
                setPlatform(e.target.value);
                setHandle("");
                setAppPassword("");
                setModalError(null);
              }}
              disabled={isSubmitting}
              className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
            >
              <option value="Bluesky">Bluesky</option>
              <option value="Twitter">X (Twitter)</option>
              <option value="LinkedIn">LinkedIn</option>
              <option value="MetaThreads">Threads</option>
              <option value="Instagram">Instagram</option>
            </select>
          </div>

          {platform === "Twitter" ? (
            <div className="pt-2 space-y-4">
              <div className="p-3 border border-border bg-bg/40 rounded-lg text-xs text-muted font-sans space-y-2">
                <div className="text-foreground font-medium flex items-center gap-2">
                  <FontAwesomeIcon icon={PLATFORM_ICONS.Twitter} className="w-3.5 h-3.5 text-foreground" />
                  <span>X (Twitter) OAuth 2.0 Connection</span>
                </div>
                <p>
                  Authorize social-worker to publish threads directly to your X account using the official Twitter API v2.
                </p>
              </div>

              <button
                type="button"
                onClick={handleTwitterConnect}
                disabled={isSubmitting}
                className="w-full inline-flex items-center justify-center bg-zinc-900 hover:bg-zinc-800 text-white font-semibold text-xs py-2.5 px-4 rounded-lg transition duration-150 disabled:opacity-50 font-mono uppercase tracking-wider gap-2 shadow-sm border border-zinc-700"
              >
                <FontAwesomeIcon icon={PLATFORM_ICONS.Twitter} className="w-4 h-4 text-white" />
                <span>
                  {isSubmitting ? "Redirecting..." : "Connect with X"}
                </span>
              </button>
              <p className="text-[11px] text-muted font-sans leading-normal text-center">
                You will be redirected to X to authorize posting permissions for your account.
              </p>
            </div>
          ) : platform === "MetaThreads" ? (
            <div className="pt-2 space-y-4">
              <div className="p-3 border border-border bg-bg/40 rounded-lg text-xs text-muted font-sans space-y-2">
                <div className="text-foreground font-medium flex items-center gap-2">
                  <FontAwesomeIcon icon={PLATFORM_ICONS.MetaThreads} className="w-3.5 h-3.5 text-foreground" />
                  <span>Threads OAuth Connection</span>
                </div>
                <p>
                  Authorize social-worker to publish threads directly to your Meta Threads profile using the official Threads API.
                </p>
              </div>

              <button
                type="button"
                onClick={handleMetaThreadsConnect}
                disabled={isSubmitting}
                className="w-full inline-flex items-center justify-center bg-zinc-800 hover:bg-zinc-700 text-white font-semibold text-xs py-2.5 px-4 rounded-lg transition duration-150 disabled:opacity-50 font-mono uppercase tracking-wider gap-2 shadow-sm border border-zinc-700"
              >
                <FontAwesomeIcon icon={PLATFORM_ICONS.MetaThreads} className="w-4 h-4 text-white" />
                <span>
                  {isSubmitting ? "Redirecting..." : "Connect Threads Profile"}
                </span>
              </button>
              <p className="text-[11px] text-muted font-sans leading-normal text-center">
                You will be redirected to Meta to authorize posting permissions for your Threads account.
              </p>
            </div>
          ) : platform === "LinkedIn" ? (
            <div className="pt-2 space-y-4">
              <div>
                <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-2">
                  Account Type
                </label>
                <div className="grid grid-cols-2 gap-2">
                  <button
                    type="button"
                    onClick={() => setLinkedInType("personal")}
                    className={`py-2 px-3 text-xs font-medium rounded-lg border text-left transition-colors ${
                      linkedInType === "personal"
                        ? "border-accent bg-accent/10 text-foreground font-semibold"
                        : "border-border bg-bg text-muted hover:text-foreground"
                    }`}
                  >
                    <div className="font-sans font-semibold">Personal Profile</div>
                    <div className="text-[10px] text-muted font-normal mt-0.5">Post to member feed</div>
                  </button>
                  <button
                    type="button"
                    onClick={() => setLinkedInType("company")}
                    className={`py-2 px-3 text-xs font-medium rounded-lg border text-left transition-colors ${
                      linkedInType === "company"
                        ? "border-accent bg-accent/10 text-foreground font-semibold"
                        : "border-border bg-bg text-muted hover:text-foreground"
                    }`}
                  >
                    <div className="font-sans font-semibold">Company Page</div>
                    <div className="text-[10px] text-muted font-normal mt-0.5">Post to organization pages</div>
                  </button>
                </div>
              </div>

              <button
                type="button"
                onClick={() => handleLinkedInConnect(linkedInType)}
                disabled={isSubmitting}
                className="w-full inline-flex items-center justify-center bg-indigo-600 hover:bg-indigo-700 text-white font-semibold text-xs py-2.5 px-4 rounded-lg transition duration-150 disabled:opacity-50 font-mono uppercase tracking-wider gap-2 shadow-sm"
              >
                <svg viewBox="0 0 24 24" className="w-4 h-4 fill-current" xmlns="http://www.w3.org/2000/svg">
                  <path d="M19 0h-14c-2.761 0-5 2.239-5 5v14c0 2.761 2.239 5 5 5h14c2.762 0 5-2.239 5-5v-14c0-2.761-2.238-5-5-5zm-11 19h-3v-11h3v11zm-1.5-12.268c-.966 0-1.75-.779-1.75-1.75s.784-1.75 1.75-1.75 1.75.779 1.75 1.75-.784 1.75-1.75 1.75zm13.5 12.268h-3v-5.604c0-3.368-4-3.113-4 0v5.604h-3v-11h3v1.765c1.396-2.586 7-2.777 7 2.476v6.759z" />
                </svg>
                <span>
                  {isSubmitting
                    ? "Redirecting..."
                    : linkedInType === "company"
                    ? "Connect Company Page"
                    : "Connect Personal Profile"}
                </span>
              </button>
              <p className="text-[11px] text-muted font-sans leading-normal text-center">
                {linkedInType === "company"
                  ? "You will be redirected to LinkedIn to authorize organization management on your Company Pages."
                  : "You will be redirected to LinkedIn to authorize posting to your personal feed."}
              </p>
            </div>
          ) : platform === "Instagram" ? (
            <div className="pt-2 space-y-4">
              <div className="p-3 border border-border bg-bg/40 rounded-lg text-xs text-muted font-sans space-y-2">
                <div className="text-foreground font-medium flex items-center gap-2">
                  <FontAwesomeIcon icon={PLATFORM_ICONS.Instagram} className="w-3.5 h-3.5 text-pink-500" />
                  <span>Instagram Professional Connection</span>
                </div>
                <p>
                  Connect your Instagram Professional (Creator or Business) account via Meta Graph API to ingest posts and media from any public creator or politician.
                </p>
              </div>

              <button
                type="button"
                onClick={handleInstagramConnect}
                disabled={isSubmitting}
                className="w-full inline-flex items-center justify-center bg-gradient-to-r from-purple-600 via-pink-600 to-amber-500 hover:opacity-90 text-white font-semibold text-xs py-2.5 px-4 rounded-lg transition duration-150 disabled:opacity-50 font-mono uppercase tracking-wider gap-2 shadow-sm"
              >
                <FontAwesomeIcon icon={PLATFORM_ICONS.Instagram} className="w-4 h-4 text-white" />
                <span>
                  {isSubmitting ? "Redirecting..." : "Connect Instagram via Meta"}
                </span>
              </button>
              <div className="relative flex py-1 items-center">
                <div className="flex-grow border-t border-border"></div>
                <span className="flex-shrink mx-3 text-muted text-[10px] font-mono uppercase">Or enter token directly</span>
                <div className="flex-grow border-t border-border"></div>
              </div>
              <form onSubmit={handleSave} className="space-y-3">
                <div>
                  <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                    Your Handle
                  </label>
                  <input
                    type="text"
                    value={handle}
                    onChange={(e) => setHandle(e.target.value)}
                    disabled={isSubmitting}
                    placeholder="@yourhandle"
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
                  />
                </div>
                <div>
                  <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                    Meta Graph API Access Token
                  </label>
                  <input
                    type="password"
                    value={appPassword}
                    onChange={(e) => setAppPassword(e.target.value)}
                    disabled={isSubmitting}
                    placeholder="EAA..."
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
                  />
                  <p className="mt-1 text-[11px] text-muted font-sans leading-normal">
                    Generate an access token with <code className="text-foreground">instagram_basic</code> and <code className="text-foreground">pages_show_list</code> permissions.
                  </p>
                </div>
                <button
                  type="submit"
                  disabled={isSubmitting || !handle || !appPassword}
                  className="w-full bg-accent hover:bg-accent/90 text-white font-semibold text-xs py-2 px-4 rounded-lg transition duration-150 disabled:opacity-50 font-mono uppercase tracking-wider"
                >
                  Save Instagram Token
                </button>
              </form>
            </div>
          ) : (
            <form onSubmit={handleSave} className="space-y-4">
              <div>
                <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                  Handle / Username
                </label>
                <input
                  type="text"
                  value={handle}
                  onChange={(e) => setHandle(e.target.value)}
                  disabled={isSubmitting}
                  placeholder="e.g. user.bsky.social"
                  className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
                />
              </div>

              <div>
                <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                  App Password
                </label>
                <input
                  type="password"
                  value={appPassword}
                  onChange={(e) => setAppPassword(e.target.value)}
                  disabled={isSubmitting}
                  placeholder="••••••••••••"
                  className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
                />
                <p className="mt-1.5 text-[11px] text-muted font-sans leading-normal">
                  Always use an App Password generated in your Bluesky settings, never your main account password.
                </p>
              </div>

              <div className="flex items-center justify-end gap-2 pt-2 border-t border-border">
                <button
                  type="button"
                  onClick={handleCloseAddModal}
                  disabled={isSubmitting}
                  className="border border-border text-foreground hover:bg-border/20 text-xs py-2 px-4 rounded-lg transition-colors font-mono uppercase tracking-wider"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting || !handle || !appPassword}
                  className="bg-accent text-bg font-medium text-xs py-2 px-4 rounded-lg hover:opacity-90 transition-opacity disabled:opacity-50 font-mono uppercase tracking-wider shadow-sm"
                >
                  {isSubmitting ? "Saving..." : "Save Connection"}
                </button>
              </div>
            </form>
          )}
        </div>
      </Modal>
    </div>
  );
}
