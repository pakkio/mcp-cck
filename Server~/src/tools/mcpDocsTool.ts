import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
import * as z from 'zod';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { Logger } from '../utils/logger.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// ============================================================================
// get_mcp_docs
// ============================================================================

const toolName = 'get_mcp_docs';
const toolDescription =
  'Lists, reads, and searches all documentation, setup guides, and ChilloutVR CCK / Sketchfab best-practice files bundled inside this MCP package (works even when Unity is not connected).';

const paramsSchema = z.object({
  action: z
    .enum(['list', 'read', 'search'])
    .default('list')
    .describe('Action to perform: "list" all bundled docs, "read" a specific doc by name/path, or "search" across all docs'),
  doc: z
    .string()
    .optional()
    .describe('Document path or filename for action="read" (e.g. "INSTALL.md", "AGENTS.md", "docs/chilloutvr-cck-integration.md", "mcp-unity-sketchfab-howto.html")'),
  query: z
    .string()
    .optional()
    .describe('Search text for action="search" (e.g. "PlayerLoop", "Master Key", "Sketchfab", "WheelCollider")'),
  offset: z
    .number()
    .int()
    .min(0)
    .default(0)
    .describe('Character offset for paginating large documents in action="read"'),
  maxChars: z
    .number()
    .int()
    .min(500)
    .max(60000)
    .default(20000)
    .describe('Maximum characters to return in action="read" (default 20000)'),
  maxResults: z
    .number()
    .int()
    .min(1)
    .max(50)
    .default(15)
    .describe('Maximum search matches to return in action="search" (default 15)'),
});

export interface BundledDocEntry {
  path: string;
  title: string;
  format: 'md' | 'html' | 'txt';
  sizeBytes: number;
  absolutePath: string;
}

const ROOT_DOC_FILES = [
  'INSTALL.md',
  'AGENTS.md',
  'README.md',
  'CLAUDE.md',
  'llms.txt',
  'LICENSE.md',
  'README-ja.md',
  'README_zh-CN.md',
];

const SUPPORTED_DOC_EXTENSIONS = new Set(['.md', '.html', '.txt']);

export function resolvePackageRoot(): string {
  const candidates: string[] = [];

  try {
    const currentDir = path.dirname(fileURLToPath(import.meta.url));
    // From Server~/build/tools or Server~/src/tools -> package root is 3 levels up
    candidates.push(path.resolve(currentDir, '..', '..', '..'));
  } catch {
    // Fallback below
  }

  candidates.push(path.resolve(process.cwd(), '..'));
  candidates.push(process.cwd());

  for (const candidate of candidates) {
    if (
      fs.existsSync(path.join(candidate, 'package.json')) &&
      (fs.existsSync(path.join(candidate, 'README.md')) || fs.existsSync(path.join(candidate, 'docs')))
    ) {
      return candidate;
    }
  }

  return candidates[0];
}

export function htmlToReadableText(html: string): string {
  return html
    .replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, ' ')
    .replace(/<style\b[^>]*>[\s\S]*?<\/style>/gi, ' ')
    .replace(/<svg\b[^>]*>[\s\S]*?<\/svg>/gi, ' ')
    .replace(/<\/?(?:p|div|section|article|header|footer|aside|main|nav|li|ul|ol|tr|table|h[1-6]|br|hr|pre|blockquote)\b[^>]*>/gi, '\n')
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/gi, ' ')
    .replace(/&amp;/gi, '&')
    .replace(/&lt;/gi, '<')
    .replace(/&gt;/gi, '>')
    .replace(/&quot;/gi, '"')
    .replace(/&#39;|&apos;/gi, "'")
    .replace(/&bull;/gi, '•')
    .replace(/&rarr;/gi, '->')
    .replace(/[ \t]+\n/g, '\n')
    .replace(/[ \t]{2,}/g, ' ')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

function extractTitle(content: string, format: 'md' | 'html' | 'txt', fallbackName: string): string {
  if (format === 'html') {
    const titleMatch = content.match(/<title[^>]*>([\s\S]*?)<\/title>/i);
    if (titleMatch?.[1]) {
      return htmlToReadableText(titleMatch[1]).replace(/\s+/g, ' ').trim();
    }
  } else {
    const headingMatch = content.match(/^#\s+(.+)$/m);
    if (headingMatch?.[1]) {
      return headingMatch[1].trim();
    }
  }
  return fallbackName;
}

export function discoverBundledDocs(packageRoot: string = resolvePackageRoot()): BundledDocEntry[] {
  const entries: BundledDocEntry[] = [];
  const seen = new Set<string>();

  const addFile = (relPath: string, absPath: string) => {
    const normalized = relPath.split(path.sep).join('/');
    if (seen.has(normalized) || !fs.existsSync(absPath)) return;
    const stat = fs.statSync(absPath);
    if (!stat.isFile()) return;
    const ext = path.extname(absPath).toLowerCase();
    if (!SUPPORTED_DOC_EXTENSIONS.has(ext)) return;

    const format = ext.slice(1) as 'md' | 'html' | 'txt';
    const raw = fs.readFileSync(absPath, 'utf8');
    const title = extractTitle(raw, format, normalized);

    seen.add(normalized);
    entries.push({
      path: normalized,
      title,
      format,
      sizeBytes: stat.size,
      absolutePath: absPath,
    });
  };

  for (const filename of ROOT_DOC_FILES) {
    addFile(filename, path.join(packageRoot, filename));
  }

  const docsDir = path.join(packageRoot, 'docs');
  if (fs.existsSync(docsDir) && fs.statSync(docsDir).isDirectory()) {
    const files = fs.readdirSync(docsDir).sort((a, b) => a.localeCompare(b));
    for (const file of files) {
      addFile(`docs/${file}`, path.join(docsDir, file));
    }
  }

  return entries;
}

function readNormalizedDocText(entry: BundledDocEntry): string {
  const raw = fs.readFileSync(entry.absolutePath, 'utf8');
  return entry.format === 'html' ? htmlToReadableText(raw) : raw;
}

function resolveDocEntry(entries: BundledDocEntry[], requested: string): BundledDocEntry | null {
  const needle = requested.trim().replace(/\\/g, '/').toLowerCase();
  if (!needle || needle.includes('..')) return null;

  // 1. Exact relative path match
  const exact = entries.find((e) => e.path.toLowerCase() === needle);
  if (exact) return exact;

  // 2. Exact basename match (with or without extension)
  const byBase = entries.find(
    (e) =>
      path.basename(e.path).toLowerCase() === needle ||
      path.basename(e.path, path.extname(e.path)).toLowerCase() === needle
  );
  if (byBase) return byBase;

  // 3. Unique substring match
  const partial = entries.filter((e) => e.path.toLowerCase().includes(needle));
  if (partial.length === 1) return partial[0];

  return null;
}

export function registerGetMcpDocsTool(server: McpServer, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: z.infer<typeof paramsSchema>) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = handleGetMcpDocs(params);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

export function handleGetMcpDocs(
  rawParams: Partial<z.infer<typeof paramsSchema>>,
  packageRoot: string = resolvePackageRoot()
): CallToolResult {
  const params = paramsSchema.parse(rawParams ?? {});
  const entries = discoverBundledDocs(packageRoot);

  if (params.action === 'list') {
    const summaryLines = entries.map(
      (e) => `- \`${e.path}\` (${(e.sizeBytes / 1024).toFixed(1)} KB, ${e.format}) — ${e.title}`
    );
    const text =
      entries.length > 0
        ? `Bundled MCP documentation (${entries.length} files):\n\n${summaryLines.join('\n')}\n\nUse action="read" with doc="<path>" or action="search" with query="<keyword>".`
        : 'No bundled documentation files were found in this MCP package.';

    return {
      content: [{ type: 'text', text }],
      structuredContent: {
        action: 'list',
        count: entries.length,
        docs: entries.map(({ path: docPath, title, format, sizeBytes }) => ({
          path: docPath,
          title,
          format,
          sizeBytes,
        })),
      },
    };
  }

  if (params.action === 'read') {
    if (!params.doc || !params.doc.trim()) {
      throw new McpUnityError(
        ErrorType.VALIDATION,
        'Parameter "doc" is required when action="read". Call action="list" first to see available docs.'
      );
    }

    const entry = resolveDocEntry(entries, params.doc);
    if (!entry) {
      throw new McpUnityError(
        ErrorType.VALIDATION,
        `Document "${params.doc}" not found in bundled docs. Available docs: ${entries.map((e) => e.path).join(', ')}`
      );
    }

    const fullText = readNormalizedDocText(entry);
    const start = Math.min(params.offset, fullText.length);
    const slice = fullText.slice(start, start + params.maxChars);
    const hasMore = start + slice.length < fullText.length;

    const header =
      `# ${entry.path} (${entry.title})\n` +
      `*(Chars ${start}–${start + slice.length} of ${fullText.length}` +
      `${hasMore ? `; pass offset=${start + slice.length} to read more` : ''})*\n\n`;

    return {
      content: [{ type: 'text', text: header + slice }],
      structuredContent: {
        action: 'read',
        doc: entry.path,
        title: entry.title,
        format: entry.format,
        offset: start,
        returnedChars: slice.length,
        totalChars: fullText.length,
        hasMore,
        nextOffset: hasMore ? start + slice.length : null,
      },
    };
  }

  // action === 'search'
  const query = params.query?.trim();
  if (!query) {
    throw new McpUnityError(
      ErrorType.VALIDATION,
      'Parameter "query" is required when action="search".'
    );
  }

  const lowerQuery = query.toLowerCase();
  const terms = lowerQuery.split(/\s+/).filter(Boolean);
  const matches: Array<{ doc: string; title: string; line: number; snippet: string }> = [];

  for (const entry of entries) {
    const text = readNormalizedDocText(entry);
    const lines = text.split(/\r?\n/);

    for (let i = 0; i < lines.length; i++) {
      const lineLower = lines[i].toLowerCase();
      if (lineLower.includes(lowerQuery) || (terms.length > 1 && terms.every((t) => lineLower.includes(t)))) {
        const snippetStart = Math.max(0, i - 1);
        const snippetEnd = Math.min(lines.length, i + 2);
        const snippet = lines.slice(snippetStart, snippetEnd).join('\n').trim().slice(0, 320);
        matches.push({
          doc: entry.path,
          title: entry.title,
          line: i + 1,
          snippet,
        });
        if (matches.length >= params.maxResults) break;
      }
    }
    if (matches.length >= params.maxResults) break;
  }

  const formatted =
    matches.length > 0
      ? `Found ${matches.length} match(es) for "${query}":\n\n` +
        matches.map((m) => `- **\`${m.doc}\`** (line ${m.line}):\n  > ${m.snippet.replace(/\n/g, '\n  > ')}`).join('\n\n')
      : `No matches found for "${query}" across ${entries.length} bundled docs.`;

  return {
    content: [{ type: 'text', text: formatted }],
    structuredContent: {
      action: 'search',
      query,
      count: matches.length,
      matches,
    },
  };
}
