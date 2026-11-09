| **Module:**         | Programming                                                |
| ------------------- | ---------------------------------------------------------- |
| **Assignment:**     | RaceDay: Part 3, Automation of Web Application (MVC front end) |
| **Student Name:**   | Qhama Hobongwana                                           |
| **Student Number:** | ST10465226                                                 |

# RaceDay (Part 3)

The web front end for the RaceDay platform: an **ASP.NET Core MVC** (.NET 8) application that talks to the RaceDay REST API, with image uploads stored in **Azure Blob Storage** (handled by the API) and the MVC app **containerised with Docker**.

## Repository layout

```
RaceDay-Part3/
├── RaceDay.Web/     ASP.NET Core MVC front end (the Part 3 deliverable)
├── RaceDay.Api/     The Part 2 API, extended with Blob Storage and a few endpoints Part 3 needs
├── Dockerfile       Builds and runs RaceDay.Web
└── RaceDay.sln
```

The original Part 2 repository is untouched. `RaceDay.Api` is a copy of it with the additions listed under [Changes made to the API](#changes-made-to-the-api).

## How it fits together

```
Browser ──► RaceDay.Web (MVC) ──HTTP + JWT──► RaceDay.Api ──► SQL Server
                                                   └────────► Azure Blob Storage
```

- The MVC project has **no database access, no Azure code and no business rules**. Every operation is an HTTP call made through one class, `Services/RaceDayApi.cs`.
- Uploaded files go from the browser to the MVC app, then to the API, which stores them in Azure Blob Storage. The MVC app never talks to Azure.
- **Session-based authentication:** on login the API returns a JWT. The MVC app keeps the token, role, name and id in the server-side session and attaches the token to every API call. Logging out clears the session.
- **Role-based navigation:** `[SessionAuthorize(Role = ...)]` protects every page. Not logged in means redirect to Log in; wrong role means an "access denied" page. Organisers and Participants see different menus and pages.

## Requirements to run

- .NET 8 SDK
- SQL Server LocalDB (installed with Visual Studio) or any SQL Server
- Docker Desktop (only for the container)
- An Azure Storage account (only for image uploads; see below)

## 1. Create the Azure Storage account (one time)

1. Sign in at <https://portal.azure.com>. Students can activate **Azure for Students** with their institution email for free credit and no credit card.
2. Search for **Storage accounts**, then **Create**.
   - Resource group: create new, for example `raceday-rg`
   - Storage account name: lowercase letters and numbers only, for example `racedaystorage123`
   - Region: closest to you. Performance: **Standard**. Redundancy: **LRS**.
   - Select **Review + create**, then **Create**.
3. Open the new account, then **Settings → Configuration** and set **Allow Blob anonymous access** to **Enabled**. Save. (This lets browsers display uploaded banner and profile images from their URL.)
4. Open **Security + networking → Access keys**, select **Show** and copy the first **Connection string**.

You do not need to create the container. The API creates `raceday-images` the first time an image is uploaded.

## 2. Run the API

From the repository root:

```bash
cd RaceDay.Api

# Store secrets outside source control
dotnet user-secrets set "BlobStorage:ConnectionString" "<paste your connection string>"
dotnet user-secrets set "Jwt:Key" "<any random text, at least 32 characters>"

# Create the database (a new one called RaceDayPart3, so Part 2's database is not touched)
dotnet tool install --global dotnet-ef        # first time only
dotnet ef migrations add AddImageColumns
dotnet ef database update

# Run on all interfaces so the Docker container can reach it
dotnet run --urls http://0.0.0.0:5235
```

Swagger is at <http://localhost:5235/swagger>.

> `dotnet ef migrations add AddImageColumns` adds the new `banner_url` and `profile_picture_url` columns. Run it once and commit the generated files.

## 3. Run the MVC app locally

In a second terminal:

```bash
cd RaceDay.Web
dotnet run
```

Open <http://localhost:5100>. The API address comes from `ApiBaseUrl` in `RaceDay.Web/appsettings.json` (default `http://localhost:5235`).

## 4. Run the MVC app in Docker

With the API running (step 2), from the repository root, **one command**:

```bash
docker build -t raceday-web . && docker run --rm -p 8080:8080 --add-host=host.docker.internal:host-gateway -e ApiBaseUrl=http://host.docker.internal:5235 raceday-web
```

Open <http://localhost:8080>.

- `ApiBaseUrl` tells the container where the API is. Inside a container `localhost` means the container itself, so the host machine is reached as `host.docker.internal`.
- The app behaves the same in the container and locally: the only difference is that setting.

## Trying it out

1. Register an **Organiser** and a **Participant** (use two browsers, or log out in between).
2. As the Organiser: **Create event** (add a banner image), add **Categories**, and look at the **Dashboard**.
3. As the Participant: browse **Upcoming events**, open one, choose a category and **Enrol**, then check **My enrolments**.
4. As the Organiser: open **Enrolments** for the event, then **Results**, enter a finish time and position and save.
5. As the Participant: open **My results** to see the finish time and position, for example "47th out of 312 finishers".
6. Open **Profile** (either role) and upload a profile picture.

## Requirements checklist

| Requirement | Where |
| ----------- | ----- |
| MVC app consuming the API, no DB access or duplicated logic | `RaceDay.Web/Services/RaceDayApi.cs` is the only data source |
| Register, login, logout through the API | `AccountController` |
| Session keeps authenticated state and role | `Services/SessionUser.cs`, `SessionAuthorizeAttribute` |
| Distinct interface per role | Role-based navbar in `Views/Shared/_Layout.cshtml`, `OrganiserController` / `ParticipantController` |
| Unauthenticated users cannot reach protected pages | `[SessionAuthorize]`, redirect to Log in |
| Organiser dashboard | `Organiser/Dashboard` |
| Events: create, edit, delete with all fields | `Organiser/Events`, `EventForm` |
| Banner upload on create and edit | `EventForm`, API `POST /api/events/{id}/banner` |
| Category management | `Organiser/Categories` |
| Enrolments per event (participant, category, status) | `Organiser/Enrolments` |
| Results entry (finish time and position) | `Organiser/Results` |
| Participant home with filters | `Home/Index` (type, date, location) |
| Event details and categories | `Home/Details` |
| Enrol with a category | `ParticipantController.Enrol` |
| My enrolments and status | `Participant/MyEnrolments` |
| My results (time, "47th out of 312") | `Participant/MyResults` |
| Profile and profile picture | `Account/Profile` |
| Azure Blob Storage in the API only | `RaceDay.Api/Services/BlobService.cs` |
| Banner displayed on event detail page | `Home/Details` |
| Docker | `Dockerfile` (multi-stage, non-root, `.dockerignore`) |
| RaceDay brand, Bootstrap, responsive | `wwwroot/css/site.css`, Bootstrap 5.3 |
| Colour-coded statuses | `Views/Shared/_StatusBadge.cshtml` |
| Graceful errors, no raw messages | `RaceDayApi.ReadError`, `Home/Error`, TempData alerts |

## Changes made to the API

All additions are backwards compatible with Part 2.

- **Azure Blob Storage:** `Services/BlobService.cs` validates (JPG/PNG/WEBP, max 5 MB, real file header check) and uploads images.
- **New endpoints:** `POST /api/events/{id}/banner`, `POST /api/users/me/picture` (multipart upload), `GET /api/events/mine` (organiser dashboard with enrolment counts).
- **New fields:** `Event.BannerUrl`, `ProfilePictureUrl` on both user types, returned in the existing responses.
- **Results:** `GET /api/users/me/results` now includes `totalFinishers` and `eventDate`; organiser enrolment lists include any existing finish time and position.
- **Filters:** `GET /api/events/upcoming` accepts `type`, `location` and `date`.

## Notes and decisions

- **Enrolment statuses:** the API uses `Registered`, `Completed` and `Withdrawn`. They are colour-coded amber, green and grey respectively.
- **Sessions are in memory.** Restarting the MVC app (or the container) logs everyone out. Fine for coursework; a production deployment would use a shared session store.
- **Dates and decimals** use the `en-US` culture in the MVC app so local and Docker runs parse them identically.

## Troubleshooting

| Problem | Fix |
| ------- | --- |
| "The RaceDay service is currently unavailable" | The API is not running, or `ApiBaseUrl` is wrong. From Docker, start the API with `--urls http://0.0.0.0:5235` and allow it through the firewall. |
| "Image storage is not configured on the server" | The `BlobStorage:ConnectionString` secret is missing (step 2). |
| "The image could not be stored right now" | Check the connection string, and that **Allow Blob anonymous access** is Enabled on the storage account. |
| Uploaded image does not display | Same setting as above: anonymous blob access must be enabled. |
