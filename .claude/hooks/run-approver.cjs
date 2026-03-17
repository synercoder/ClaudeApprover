const { execFileSync } = require("child_process");
const { join } = require("path");

const workDir = join(process.env.CLAUDE_PROJECT_DIR, ".claude", "hooks");

try {
  execFileSync("dotnet", ["run", "-v", "q", "./Approver.cs"], {
    cwd: workDir,
    stdio: "inherit",
  });
} catch (err) {
  process.exit(err.status ?? 1);
}
