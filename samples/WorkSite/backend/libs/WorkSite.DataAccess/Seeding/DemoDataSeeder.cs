using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.PostgresClient;

namespace WorkSite.DataAccess.Seeding;

/// <summary>
/// Creates the demo account: a user with a handful of clients, several projects each, and about
/// four months of time entries generated backwards from a given date.
/// </summary>
/// <remarks>
/// The history is deterministic: the same "today" always produces the same entries. Work happens
/// mostly on weekdays, with varied hours, notes and a mix of billable and non-billable time. The
/// seeder runs only when no user with the demo username exists, and never changes existing data.
/// </remarks>
public sealed class DemoDataSeeder
{
    /// <summary>How many days of history are generated, ending on the requested date.</summary>
    public const int HistoryDays = 120;

    /// <summary>The fixed seed of the random generator, so every run produces the same history.</summary>
    public const int RandomSeed = 20260412;

    private const long SeedLockKey = 0x576F726B44656D6F;

    private static readonly (string Name, string Contact, string Email, string Color, string Notes, bool Archived,
        (string Name, string Description, decimal? Rate, bool Billable, bool Archived, int Weight, string[] Tasks)[] Projects)[] Clients =
    [
        ("Harbor Light Coffee", "Maya Chen", "maya.chen@harborlight.example", "amber",
            "Three cafes and an online shop. Invoices go out on the first of the month.", false,
        [
            ("Website refresh", "New theme, menu pages and an events calendar.", 120m, true, false, 5,
                ["Homepage layout", "Menu page content", "Events calendar", "Image optimisation", "Review call with Maya", "Accessibility fixes"]),
            ("Loyalty app", "A stamp-card app for regular customers.", 135m, true, false, 6,
                ["Sign-up flow", "Stamp scanning", "Rewards screen", "API endpoints", "Push notifications", "Bug fixes from testing"]),
            ("Monthly support", "Hosting, updates and small changes.", 95m, true, false, 2,
                ["Plugin updates", "Backup check", "Opening-hours change", "Email forwarding"]),
        ]),
        ("Cedar & Pine Architects", "Owen Fraser", "owen@cedarpine.example", "indigo",
            "Prefers a short written summary after each meeting.", false,
        [
            ("Project portal", "A private portal where their clients follow each build.", 140m, true, false, 6,
                ["Document upload", "Progress timeline", "Client permissions", "Design review", "Status meeting", "Search"]),
            ("3D viewer prototype", "Try out showing models in the browser.", 150m, true, false, 3,
                ["Model loading", "Camera controls", "Performance testing", "Demo for the partners"]),
        ]),
        ("Summit Trail Outfitters", "Priya Raman", "priya.raman@summittrail.example", "teal",
            "Busy season runs from spring to early autumn.", false,
        [
            ("Inventory dashboard", "Stock levels across the warehouse and two shops.", 125m, true, false, 5,
                ["Stock level charts", "Low-stock alerts", "Data import", "Filter by location", "Weekly sync call"]),
            ("Checkout performance", "Speed up the online checkout.", 130m, true, false, 3,
                ["Profiling", "Query tuning", "Caching", "Load testing", "Write-up of results"]),
            ("Spring campaign pages", "Landing pages for the spring sale.", 110m, true, true, 2,
                ["Landing page build", "Copy changes", "Tracking set-up"]),
        ]),
        ("Riverbend Community Clinic", "Sam Okafor", "sam.okafor@riverbendclinic.example", "green",
            "Non-profit rate. Volunteer scheduling is donated time.", false,
        [
            ("Appointment reminders", "Text and email reminders for appointments.", 85m, true, false, 3,
                ["Reminder templates", "Scheduling rules", "Opt-out handling", "Testing with staff"]),
            ("Volunteer scheduling", "A rota for clinic volunteers.", null, false, false, 2,
                ["Shift calendar", "Sign-up form", "Training session"]),
        ]),
        ("Old Mill Bakery", "Lena Hoffman", "lena@oldmillbakery.example", "orange",
            "Finished. Kept for the records.", true,
        [
            ("Online ordering", "Pre-orders for collection.", 100m, true, true, 3,
                ["Order form", "Collection times", "Handover"]),
        ]),
        ("Internal", "", "", "slate",
            "Time that is not billed to anyone.", false,
        [
            ("Admin and planning", "Invoicing, email and planning the week.", null, false, false, 2,
                ["Invoicing", "Planning the week", "Email", "Bookkeeping"]),
            ("Learning", "Courses, reading and experiments.", null, false, false, 1,
                ["Reading", "Online course", "Trying a new library"]),
        ]),
    ];

    private readonly PgSqlDataSource _dataSource;

    /// <summary>Creates the seeder.</summary>
    /// <param name="dataSource">The application database.</param>
    public DemoDataSeeder(PgSqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    /// <summary>Creates the demo account and its history, unless a user with that username exists.</summary>
    /// <param name="request">The demo user's details and the date the history ends on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What was created.</returns>
    public async Task<DemoSeedResult> SeedAsync(DemoSeedRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.PasswordHash))
        {
            throw new ArgumentException("The demo account needs a username and a password hash.", nameof(request));
        }

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        //Two instances starting together must not both seed.
        await connection.ExecuteAsync("SELECT pg_advisory_xact_lock(@key)", new { key = SeedLockKey }, transaction,
            cancellationToken);

        var existing = await connection.ExecuteScalarAsync<Guid?>(
            "SELECT id FROM users WHERE lower(username) = lower(@username)",
            new { username = request.Username }, transaction, cancellationToken);
        if (existing.HasValue)
        {
            return new DemoSeedResult { Created = false, UserId = existing.Value };
        }

        var now = DateTime.UtcNow;
        var start = request.Today.AddDays(-(HistoryDays - 1));
        var userId = Guid.CreateVersion7();
        await connection.ExecuteAsync(
            "INSERT INTO users (id, first_name, last_name, email, username, password_hash, is_demo, created_at) " +
            "VALUES (@userId, @firstName, @lastName, @email, @username, @passwordHash, true, @createdAt)",
            new
            {
                userId,
                firstName = request.FirstName,
                lastName = request.LastName,
                email = request.Email,
                username = request.Username,
                passwordHash = request.PasswordHash,
                createdAt = Earlier(AtUtc(start.AddDays(-7), 9), now),
            },
            transaction, cancellationToken);

        var result = new DemoSeedResult { Created = true, UserId = userId };
        var projects = new List<(Guid Id, bool Billable, bool Archived, int Weight, string[] Tasks)>();
        var clientIndex = 0;
        foreach (var client in Clients)
        {
            var clientId = Guid.CreateVersion7();
            await connection.ExecuteAsync(
                "INSERT INTO clients (id, user_id, name, contact_name, contact_email, notes, color, is_archived, created_at) " +
                "VALUES (@clientId, @userId, @name, @contactName, @contactEmail, @notes, @color, @isArchived, @createdAt)",
                new
                {
                    clientId,
                    userId,
                    name = client.Name,
                    contactName = client.Contact,
                    contactEmail = client.Email,
                    notes = client.Notes,
                    color = client.Color,
                    isArchived = client.Archived,
                    createdAt = Earlier(AtUtc(start.AddDays(-6 + clientIndex), 10), now),
                },
                transaction, cancellationToken);
            result.ClientCount++;

            foreach (var project in client.Projects)
            {
                var projectId = Guid.CreateVersion7();
                await connection.ExecuteAsync(
                    "INSERT INTO projects (id, user_id, client_id, name, description, hourly_rate, is_billable, is_archived, created_at) " +
                    "VALUES (@projectId, @userId, @clientId, @name, @description, @hourlyRate::numeric, @isBillable, @isArchived, @createdAt)",
                    new
                    {
                        projectId,
                        userId,
                        clientId,
                        name = project.Name,
                        description = project.Description,
                        hourlyRate = project.Rate,
                        isBillable = project.Billable,
                        isArchived = project.Archived,
                        createdAt = Earlier(AtUtc(start.AddDays(-6 + clientIndex), 11), now),
                    },
                    transaction, cancellationToken);
                projects.Add((projectId, project.Billable, project.Archived || client.Archived, project.Weight, project.Tasks));
                result.ProjectCount++;
            }

            clientIndex++;
        }

        foreach (var entry in GenerateEntries(start, request.Today, projects))
        {
            await connection.ExecuteAsync(
                "INSERT INTO time_entries (id, user_id, project_id, work_date, hours, description, is_billable, created_at, updated_at) " +
                "VALUES (@entryId, @userId, @projectId, @workDate, @hours, @description, @isBillable, @createdAt, @createdAt)",
                new
                {
                    entryId = Guid.CreateVersion7(),
                    userId,
                    projectId = entry.ProjectId,
                    workDate = entry.WorkDate,
                    hours = entry.Hours,
                    description = entry.Description,
                    isBillable = entry.Billable,
                    createdAt = Earlier(AtUtc(entry.WorkDate, 16).AddMinutes(entry.Sequence * 7), now),
                },
                transaction, cancellationToken);
            result.EntryCount++;
            result.TotalHours += entry.Hours;
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Generates the demo history. Archived projects only have entries in the older part of the range.
    /// </summary>
    private static IEnumerable<(Guid ProjectId, DateOnly WorkDate, decimal Hours, string Description, bool Billable, int Sequence)>
        GenerateEntries(DateOnly start, DateOnly end, List<(Guid Id, bool Billable, bool Archived, int Weight, string[] Tasks)> projects)
    {
        var random = new Random(RandomSeed);
        var archiveCutoff = start.AddDays(HistoryDays / 2);
        var sequence = 0;

        for (var day = start; day <= end; day = day.AddDays(1))
        {
            var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var active = projects.Where(p => !p.Archived || day < archiveCutoff).ToList();
            var billableWork = active.Where(p => p.Billable).ToList();
            var internalWork = active.Where(p => !p.Billable).ToList();

            if (weekend)
            {
                if (random.NextDouble() < 0.12)
                {
                    var project = PickWeighted(random, billableWork);
                    yield return Entry(random, project, day, Quarters(random, 4, 12), ++sequence);
                }

                continue;
            }

            if (random.NextDouble() < 0.06)
            {
                continue;
            }

            //A working day of six to eight and a half hours, split over two to four projects.
            var remaining = Quarters(random, 24, 34);
            if (internalWork.Count > 0 && random.NextDouble() < 0.35)
            {
                var hours = Quarters(random, 2, 6);
                remaining -= hours;
                yield return Entry(random, PickWeighted(random, internalWork), day, hours, ++sequence);
            }

            var pieces = random.Next(2, 4);
            var chosen = new List<(Guid Id, bool Billable, bool Archived, int Weight, string[] Tasks)>();
            while (chosen.Count < pieces && chosen.Count < billableWork.Count)
            {
                var candidate = PickWeighted(random, billableWork);
                if (!chosen.Contains(candidate))
                {
                    chosen.Add(candidate);
                }
            }

            for (var i = 0; i < chosen.Count; i++)
            {
                var hours = i == chosen.Count - 1
                    ? remaining
                    : Math.Min(remaining - 0.5m * (chosen.Count - 1 - i), Quarters(random, 4, 16));
                if (hours <= 0)
                {
                    break;
                }

                remaining -= hours;
                yield return Entry(random, chosen[i], day, hours, ++sequence);
            }
        }
    }

    private static (Guid ProjectId, DateOnly WorkDate, decimal Hours, string Description, bool Billable, int Sequence) Entry(
        Random random, (Guid Id, bool Billable, bool Archived, int Weight, string[] Tasks) project, DateOnly day,
        decimal hours, int sequence)
    {
        var description = project.Tasks[random.Next(project.Tasks.Length)];
        //Now and then billable work is written off: internal reviews, fixing our own mistakes.
        var billable = project.Billable && random.NextDouble() >= 0.08;
        if (project.Billable && !billable)
        {
            description += " (not billed)";
        }

        return (project.Id, day, hours, description, billable, sequence % 8);
    }

    private static (Guid Id, bool Billable, bool Archived, int Weight, string[] Tasks) PickWeighted(Random random,
        List<(Guid Id, bool Billable, bool Archived, int Weight, string[] Tasks)> projects)
    {
        var total = projects.Sum(p => p.Weight);
        var pick = random.Next(total);
        foreach (var project in projects)
        {
            if (pick < project.Weight)
            {
                return project;
            }

            pick -= project.Weight;
        }

        return projects[projects.Count - 1];
    }

    /// <summary>A random number of quarter hours between the bounds, inclusive, as hours.</summary>
    private static decimal Quarters(Random random, int minQuarters, int maxQuarters)
        => random.Next(minQuarters, maxQuarters + 1) * 0.25m;

    private static DateTime AtUtc(DateOnly day, int hour)
        => day.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc);

    private static DateTime Earlier(DateTime a, DateTime b) => a < b ? a : b;
}
