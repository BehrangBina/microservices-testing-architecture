# GitHub Actions Troubleshooting Guide

## Common Issue: Action Download Failures

### Symptoms
```
Error: An action could not be found at the URI 'https://codeload.github.com/...'
Error: Failed to download archive after 1 attempts.
```

### Root Cause
This error occurs when GitHub Actions runners cannot download action dependencies from GitHub's codeload servers. This is typically a **temporary infrastructure issue** with GitHub, not a problem with your workflow or code.

### Solutions

#### 1. **Immediate Solution: Retry the Workflow**
- Go to the Actions tab in your repository
- Click on the failed workflow run
- Click "Re-run all jobs" or "Re-run failed jobs"
- The error usually resolves itself within minutes to hours

#### 2. **Manual Trigger**
The workflow now supports manual triggering via `workflow_dispatch`:
- Go to Actions ? CI Pipeline
- Click "Run workflow"
- Select your branch and click "Run workflow"

#### 3. **Check GitHub Status**
If the problem persists:
- Visit [GitHub Status](https://www.githubstatus.com/)
- Check for any ongoing incidents with GitHub Actions

### Recent Improvements

The workflow has been enhanced with:

1. **Timeouts**: Each job has explicit timeout limits to prevent hanging
2. **Better Caching**: Improved NuGet package caching with OS-specific keys
3. **Manual Triggers**: Added `workflow_dispatch` for on-demand workflow execution
4. **Better Step Names**: More descriptive step names for easier debugging

### Workflow Structure

```yaml
Jobs:
??? build (15 min timeout)
??? contract-tests (10 min timeout, depends on build)
??? api-tests (depends on build)
??? integration-tests (depends on build)
??? event-tests (depends on build)
??? e2e-tests (depends on all previous test jobs)
```

### When to Investigate Further

If the errors persist for more than a few hours:

1. **Check Rate Limits**: Ensure you're not hitting GitHub API rate limits
2. **Network Issues**: Verify your organization's network/firewall isn't blocking GitHub
3. **Action Versions**: Verify action versions are current (already using latest):
   - `actions/checkout@v4`
   - `actions/setup-dotnet@v4`
   - `actions/upload-artifact@v4`
   - `actions/cache@v4`
   - `dorny/test-reporter@v1`

### Related Issues
- The code changes made to fix build errors (Program class ambiguity) are unrelated to these download failures
- The DateTime serialization fix for Pact tests is also unrelated

### Support Resources
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [GitHub Actions Community Forum](https://github.community/c/code-to-cloud/github-actions/)
- [GitHub Status Page](https://www.githubstatus.com/)
