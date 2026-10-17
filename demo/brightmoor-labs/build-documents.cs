#:package DocumentFormat.OpenXml

// Writes the Word documents of the Brightmoor Labs demo: dotnet run demo/brightmoor-labs/build-documents.cs
// They use real heading styles, numbering, tables, tracked changes and code set in Consolas, as Word saves them.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

var folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(AppContext.GetData("EntryPointFilePath") as string ?? "."))!, "documents");
Directory.CreateDirectory(folder);

Save(Path.Combine(folder, "employee-handbook.docx"), Handbook());
Save(Path.Combine(folder, "support-contract.docx"), Contract());
Save(Path.Combine(folder, "developer-onboarding.docx"), Onboarding());
Console.WriteLine($"Wrote three documents to {folder}");

static IEnumerable<OpenXmlElement> Handbook() =>
[
    Styled("Title", "Brightmoor Labs Employee Handbook"),
    Plain("This handbook collects the rules that apply to everyone at Brightmoor Labs. Where a contract says something different, the contract wins."),
    Styled("Heading1", "Working time"),
    Plain("Core hours are from 10:00 to 15:00; outside them you plan your day yourself. Overtime goes to your flexitime account, which may hold up to 40 hours."),
    Styled("Heading1", "Travel and expenses"),
    Plain("Book every trip through the travel portal so that the company insurance covers it. Keep all receipts; the portal asks for a photo of each one."),
    Styled("Heading2", "Trains"),
    Plain("For trips inside Germany and to neighbouring countries the train is the default. Planes need the approval of your lead and are only an option when the train journey would take more than twice as long. The travel portal shows both options side by side, including the emissions of each, so you can see the difference at a glance."),
    Plain("Book as early as you can: saver fares are cheaper and are reimbursed in full even when they cannot be cancelled, as long as the trip was approved. If a meeting moves and the ticket is lost, that is a cost of doing business, not your problem. Seat reservations are reimbursed as well, and on long journeys you should always make one."),
    Plain("Delays happen. When a train is late and you miss a connection, take the next one and keep the confirmation of the delay; you do not need to ask anyone. Taxis to and from the station are fine when no public transport runs or when you carry equipment for a customer."),
    Plain("Work on the train counts as working time, and so does the journey itself when it falls on a working day. If you travel on a weekend because the customer asked for it, you receive a day off in return; enter the trip in the time tracking tool and mark it as weekend travel so that the People team can book the day."),
    Plain("Bikes may travel with you on regional trains, and the ticket for the bike is reimbursed. Luggage beyond a suitcase, such as demo hardware for a trade fair, should be sent ahead with the logistics partner instead of being carried; the office team arranges the pickup."),
    Plain("Second class is the standard. First class is allowed when a single journey lasts more than 4 hours, and the company pays for a BahnCard 50 when you travel often enough for it to pay off."),
    Styled("Heading2", "Hotels"),
    Plain("Stay close to where you work during the trip, not close to the city centre. A short walk in the morning saves more time than a nicer neighbourhood, and the travel portal sorts hotels by distance to the address you enter. Prefer hotels with free cancellation; the small extra cost is worth it because plans change."),
    Plain("If you travel with colleagues, you do not have to stay in the same hotel, and nobody shares a room. Longer stays of a week or more can be booked as an apartment when that is cheaper than a hotel; ask the People team, who have contracts with providers in the cities we visit most."),
    Plain("Breakfast is part of the room price and should be booked with it. Extras such as the minibar, laundry for short trips or a room upgrade are private costs. Parking at the hotel is reimbursed when you travel by car with the approval of your lead."),
    Plain("Loyalty points that you collect on business stays are yours to keep, but they must not influence the choice of the hotel. If the cheaper hotel is equally close, book the cheaper one even if another chain would earn you points."),
    Plain("Late check-out is worth asking for on the day you leave, especially when your train departs in the evening. If the hotel charges for it, the cost is reimbursed when it is lower than the price of a day room near the station."),
    Plain("The room may cost up to 140 euros per night in Germany and up to 180 euros per night abroad, breakfast included. During trade fairs prices rise; ask your lead before you book above these limits."),
    Styled("Heading2", "Meals"),
    Plain("For each full day away from home you receive a meal allowance of 28 euros; on the day you arrive and the day you leave it is 14 euros. Meals paid by a customer or included in a conference ticket reduce the allowance."),
    Styled("Heading1", "Remote work"),
    Plain("You may work from home on up to 3 days a week. Working from another EU country is possible for up to 20 working days per year; tell the People team two weeks before you go."),
    Styled("Heading1", "Your first week"),
    Numbered("Collect your laptop and badge at the front desk."),
    Numbered("Set up two-factor authentication for every account."),
    Numbered("Book a lunch with your buddy."),
    Numbered("Read this handbook and the security policy."),
];

static IEnumerable<OpenXmlElement> Contract() =>
[
    Styled("Title", "Support and Service Level Agreement"),
    Plain("This agreement between Brightmoor Labs GmbH and the customer describes how Brightmoor supports the Brightmoor Sync service."),
    Styled("Heading1", "Support priorities"),
    Plain("The customer sets the priority when opening a ticket. Brightmoor may change it after the first response and explains why."),
    Table(
        ["Priority", "Example", "First response"],
        ["P1", "Sync has stopped for all users", "1 hour, 24/7"],
        ["P2", "Sync is slow or fails for some folders", "4 business hours"],
        ["P3", "A question about configuration", "1 business day"],
        ["P4", "A feature request", "3 business days"]),
    Styled("Heading1", "Availability"),
    Plain("Brightmoor guarantees an availability of 99.9 percent per calendar month. For each started 0.1 percentage point below that, the customer receives a service credit of 10 percent of the monthly fee, capped at 30 percent of the monthly fee."),
    Styled("Heading1", "Maintenance windows"),
    Plain("Planned maintenance takes place on Sundays between 02:00 and 05:00 CET and is announced at least 5 days in advance."),
    Styled("Heading1", "Term and termination"),
    Revised("The agreement renews every 12 months. Either party may terminate it with ", "four weeks'", "three months'", " notice to the end of a calendar quarter."),
];

static IEnumerable<OpenXmlElement> Onboarding() =>
[
    Styled("Title", "Developer Onboarding"),
    Plain("Welcome to the sync agent team. This page gets you from a fresh laptop to a passing test run."),
    Styled("Heading1", "Accounts"),
    Numbered("Sign in to git.brightmoor.dev with your company account."),
    Numbered("Ask your lead for the Engineering vault in 1Password."),
    Numbered("Join the #sync-agent channel."),
    Styled("Heading1", "Local setup"),
    Plain("Clone the repository and let the bootstrap script install the toolchain:"),
    Monospace("git clone git@git.brightmoor.dev:core/sync-agent.git"),
    Monospace("cd sync-agent"),
    Monospace("make bootstrap"),
    Plain("Point the agent at the local environment before you run it:"),
    Styled("Code", "export BRIGHTMOOR_ENV=local"),
    Styled("Heading1", "Tests"),
    Plain("make test runs the unit tests in about two minutes. make e2e starts the end-to-end suite, which needs Docker and takes about 12 minutes."),
    Styled("Heading1", "Code review"),
    Plain("Every change goes through a merge request. Changes under core/ need two approvals; everything else needs one."),
    Styled("Heading1", "On-call"),
    Plain("After your third month you join the on-call rotation, one week in six. Each week on call is paid with an allowance of 250 euros."),
];

static Paragraph Plain(string text) => new(TextRun(text));

static Paragraph Styled(string style, string text) =>
    new(new ParagraphProperties(new ParagraphStyleId { Val = style }), TextRun(text));

static Paragraph Monospace(string text) =>
    new(new Run(new RunProperties(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" }), Literal(text)));

static Paragraph Numbered(string text) =>
    new(new ParagraphProperties(
            new ParagraphStyleId { Val = "ListParagraph" },
            new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = 1 })),
        TextRun(text));

static Paragraph Revised(string before, string deleted, string inserted, string after) =>
    new(
        TextRun(before),
        new DeletedRun(new Run(new DeletedText(deleted) { Space = SpaceProcessingModeValues.Preserve })) { Author = "Legal", Id = "1" },
        new InsertedRun(TextRun(inserted)) { Author = "Legal", Id = "2" },
        TextRun(after));

static Table Table(params string[][] rows)
{
    var borders = new TableBorders(
        new TopBorder { Val = BorderValues.Single, Size = 4 }, new LeftBorder { Val = BorderValues.Single, Size = 4 },
        new BottomBorder { Val = BorderValues.Single, Size = 4 }, new RightBorder { Val = BorderValues.Single, Size = 4 },
        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 }, new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 });
    var table = new Table(new TableProperties(borders), new TableGrid(rows[0].Select(_ => new GridColumn { Width = "3000" })));
    foreach (var row in rows)
        table.Append(new TableRow(row.Select(cell => new TableCell(new Paragraph(TextRun(cell))))));
    return table;
}

static Run TextRun(string text) => new(Literal(text));

static Text Literal(string text) => new(text) { Space = SpaceProcessingModeValues.Preserve };

static void Save(string path, IEnumerable<OpenXmlElement> body)
{
    using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
    var main = document.AddMainDocumentPart();
    main.Document = new Document(new Body(body));
    main.AddNewPart<StyleDefinitionsPart>().Styles = Styles();
    main.AddNewPart<NumberingDefinitionsPart>().Numbering = new Numbering(
        new AbstractNum(new Level(new StartNumberingValue { Val = 1 }, new NumberingFormat { Val = NumberFormatValues.Decimal }, new LevelText { Val = "%1." })
            { LevelIndex = 0 }) { AbstractNumberId = 0 },
        new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = 1 });
}

static Styles Styles() => new(
    Style("Normal", "Normal", isDefault: true),
    Style("Title", "Title", size: 52),
    Style("Heading1", "heading 1", size: 32, outline: 0),
    Style("Heading2", "heading 2", size: 26, outline: 1),
    Style("ListParagraph", "List Paragraph"),
    Style("Code", "Code", font: "Consolas"));

static Style Style(string id, string name, bool isDefault = false, int? size = null, int? outline = null, string? font = null)
{
    var style = new Style(new StyleName { Val = name }) { Type = StyleValues.Paragraph, StyleId = id, Default = isDefault ? true : null };
    if (id != "Normal")
        style.Append(new BasedOn { Val = "Normal" });
    if (outline is { } level)
        style.Append(new StyleParagraphProperties(new OutlineLevel { Val = level }));

    var run = new StyleRunProperties();
    if (size is { } halfPoints)
        run.Append(new Bold(), new FontSize { Val = halfPoints.ToString(System.Globalization.CultureInfo.InvariantCulture) });
    if (font is not null)
        run.Append(new RunFonts { Ascii = font, HighAnsi = font });
    if (run.HasChildren)
        style.Append(run);
    return style;
}
