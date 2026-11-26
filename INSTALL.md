# Installing MCP CCK (`com.pakkio.mcp-cck`)

MCP CCK connects AI assistants (Claude Code, Cursor, Windsurf, VS Code / GitHub Copilot, Codex CLI, Antigravity, OpenCode) to Unity Editor with native **ChilloutVR CCK** tools and Blender bridge support.

---

## Prerequisites

- **Unity Editor:**
  - For **ChilloutVR CCK 4**: **Unity `2022.3.58f1`** with **3D (Built-In Render Pipeline)** (check live requirement anytime at `https://docs.chilloutvr.net/cck/setup/` or run `howto_cck(topic="setup")`).
  - For general Unity projects: Unity 2022.3 LTS or Unity 6+.
- **Node.js 18+** and **npm 9+** (required to run the MCP stdio server).

---

## Option A — Install from Git URL (Recommended)

1. Open your Unity project.
2. Go to **Window > Package Manager**.
3. Click **`+`** (top-left) → **Add package from git URL...**
4. Paste:
   ```text
   https://github.com/pakkio/mcp-cck.git
   ```
   *(Or pin to a specific release tag: `https://github.com/pakkio/mcp-cck.git#v1.8.1`)*
5. Click **Add** and wait for Unity to import and compile.

---

## Option B — Install from Release `.zip` (`mcp-cck-v1.8.1.zip`)

1. Download **`mcp-cck-v1.8.1.zip`** from the [GitHub Releases page](https://github.com/pakkio/mcp-cck/releases/latest).
2. Extract the archive so you have the `com.pakkio.mcp-cck` folder (either outside your project or directly inside your Unity project's `Packages/` folder).
3. Install Node dependencies for the MCP server (one time):
   ```bash
   cd com.pakkio.mcp-cck/Server~
   npm install --omit=dev
   ```
   *(The compiled JS output `Server~/build/index.js` is already included in the release zip — no build step required. Run `npm install && npm run build` only if you modify TypeScript sources.)*
4. Add the package in Unity:
   - If you extracted it **outside** `Packages/`: open **Window > Package Manager** → **`+`** → **Add package from disk...** → select `com.pakkio.mcp-cck/package.json`.
   - If you extracted it directly into **`<YourUnityProject>/Packages/com.pakkio.mcp-cck/`**: Unity detects and imports it automatically on focus.

---

## Connect Your AI MCP Client

### 1. Automatic (Recommended)
1. In Unity, open **Tools > MCP Unity > Server Window**.
2. Confirm the WebSocket server is running on `ws://localhost:8090/McpUnity`.
3. Click the **Configure** button for your AI client (prefer the **(Project)** button if available so the config uses portable relative paths).

### 2. Manual JSON Configuration
Add the server entry to your client's MCP config (e.g. `.mcp.json`, `.vscode/mcp.json`, `.cursor/mcp.json`, or `~/.claude.json`):

```json
{
  "mcpServers": {
    "mcp-cck": {
      "command": "node",
      "args": [
        "ABSOLUTE/PATH/TO/com.pakkio.mcp-cck/Server~/build/index.js"
      ]
    }
  }
}
```

---

## Verify Setup

Once connected, ask your AI assistant:
- `get_mcp_docs(action="list")` / `get_mcp_docs(action="search", query="Sketchfab")` — lists, reads, and searches all bundled documentation & best-practice guides (works even without Unity open).
- `howto_cck(topic="setup")` — shows the live official ChilloutVR CCK & Unity version from `api.chilloutvr.net`.
- `inspect_cvr_cck(action="check_setup")` — audits your Unity version, Built-In Render Pipeline, Color Space, and CCK installation.
