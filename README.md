[![Lifecycle:Stable](https://img.shields.io/badge/Lifecycle-Stable-97ca00)](https://github.com/bcgov/repomountie/blob/master/doc/lifecycle-badges.md) The codebase is being roughed out, but finer details are likely to change.

# RfcBuddy
The RFC Buddy takes the 365-day change schedule published daily by the OCIO and applies filters and highlights based on keywords. The current schedule is downloaded automatically. It then provides a Word document that's pre-formatted for easy reference during CAB meetings.

# Quick Start
1. Clone the repository
1. Open the solution in Visual Studio
1. Create an appSettings.Development.json file based on the existing appSettings.json file to provide KeyCloak settings and download details for the 365-day change schedule. Reach out to one of the repo maintainers for those details of necessary.
1. Run it.

# REST API and PATs
RfcBuddy now includes a PAT-authenticated REST API for downstream systems.

- Sign in to the web app and open the Tokens page to create, view, and revoke personal access tokens.
- The first interactive user who signs in is promoted to administrator automatically, and administrators can manage other users and revoke tokens from the Admin page.
- Send a POST request to /api/v1/rfcs/search with an Authorization header using the Bearer scheme.
- The request body accepts include and ignore keyword arrays and returns a JSON payload with matched RFCs and change status.
