import { defineConfig, devices } from '@playwright/test';

const apiPort = 5032;
const fullPort = 64028;

export default defineConfig({
  testDir: './e2e/full',
  globalSetup: './e2e/full/global-setup.ts',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: 'list',
  use: {
    ...devices['Desktop Chrome'],
    baseURL: `http://localhost:${fullPort}`,
    trace: 'on-first-retry',
  },
  webServer: [
    {
      command: `dotnet run --project VibeTest.Server/VibeTest.Server.csproj --no-launch-profile --urls http://localhost:${apiPort}`,
      cwd: '..',
      env: {
        ASPNETCORE_ENVIRONMENT: 'E2E',
        ConnectionStrings__DefaultConnection:
          process.env.ConnectionStrings__DefaultConnection
          ?? 'Host=localhost;Port=5432;Database=vibetest_e2e;Username=vibetest;Password=changeme',
      },
      url: `http://localhost:${apiPort}/api/tests`,
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
    },
    {
      command: `npm run build:e2e && npx vite preview --mode e2e --port ${fullPort} --host`,
      url: `http://localhost:${fullPort}`,
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
    },
  ],
});
