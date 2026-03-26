using Microsoft.AspNetCore.Mvc.RazorPages;
using ProductNormaliser.Web.Models;

namespace ProductNormaliser.Web.Pages;

public class HelpModel : PageModel
{
    public PageHeroModel Hero { get; } = new()
    {
        Eyebrow = "Operator Guide",
        Title = "Navigate the operator journeys with less guesswork",
        Description = "This guide maps the console to the jobs operators actually need to do: choose categories, prepare sources, launch crawls, inspect products, review quality, and triage runtime issues without hunting through every page in order.",
        Metrics =
        [
            new HeroMetricModel { Label = "Help categories", Value = "6" },
            new HeroMetricModel { Label = "Guided journeys", Value = "12" },
            new HeroMetricModel { Label = "Common questions", Value = "18" },
            new HeroMetricModel { Label = "Primary workflow", Value = "Boot and populate" }
        ]
    };

    public IReadOnlyList<HelpSectionModel> Sections { get; } =
    [
        new HelpSectionModel
        {
            Id = "your-first-run",
            Title = "Your First Run",
            AudienceHint = "Start here",
            Summary = "Use this when the environment is new, the source registry is thin, or you want the shortest safe path from empty console to first useful crawl.",
            Links =
            [
                new HelpLinkModel { Label = "Dashboard", Page = "/Index" },
                new HelpLinkModel { Label = "Categories", Page = "/Categories/Index" },
                new HelpLinkModel { Label = "Sources", Page = "/Sources/Index" }
            ],
            Journeys =
            [
                new HelpJourneyModel
                {
                    Title = "Stand up the first crawl",
                    Outcome = "You finish with selected categories, at least one usable source, and a crawl job running with something real to monitor.",
                    Steps =
                    [
                        "Open Categories and select the families you want to operate, such as TVs, Monitors, or Laptops.",
                        "Check the selection summary and schema-completeness scores so you know whether the chosen categories are mature enough for a first pass.",
                        "Go to Sources and either register one or more known hosts or use candidate discovery to build an initial registry more quickly.",
                        "Enable the sources you trust first and confirm they are discovery-configured or otherwise close to boot-ready.",
                        "Launch the crawl from Categories or the Dashboard and then keep the Dashboard and Crawl Jobs views open together for the first run."
                    ]
                },
                new HelpJourneyModel
                {
                    Title = "Avoid the most common first-run mistakes",
                    Outcome = "You reduce false starts caused by weak category readiness, empty sources, or expectations the current UI does not actually support.",
                    Steps =
                    [
                        "Do not treat category selection as enough on its own; selected families still need sources that support them.",
                        "Treat schema completeness as a readiness signal before you judge quality output from a crawl.",
                        "If the required versus optional attribute rules are wrong for a category, adjust them from the Dashboard quality summary so the managed platform schema changes before you rely on downstream quality metrics.",
                        "Start with a narrow source set you understand, then expand once discovery and product confirmation look credible.",
                        "Use operator-assisted onboarding first unless you already trust the automation thresholds for your environment."
                    ]
                }
            ],
            Questions =
            [
                new HelpQuestionModel
                {
                    Question = "What is the intended Milestone 1 startup path?",
                    Answer = "Boot the hosts, register or enable sources, choose categories, launch a seeded category crawl, then watch discovery queue depth, confirmed product targets, and canonical product counts move together from the Dashboard and Crawl Jobs views."
                },
                new HelpQuestionModel
                {
                    Question = "Why should I care about schema completeness before my first crawl?",
                    Answer = "A crawl can still run with imperfect category coverage, but low completeness means the resulting products and quality metrics may look weaker for structural reasons rather than because the sources themselves are poor."
                },
                new HelpQuestionModel
                {
                    Question = "Can I configure required and optional fields from this UI?",
                    Answer = "Yes. On the Dashboard quality summary, toggling an attribute between optional and required saves directly to the managed platform schema behind the scenes, so future quality review and population runs use the updated rule."
                }
            ]
        },
        new HelpSectionModel
        {
            Id = "categories-and-schema",
            Title = "Categories And Schema Readiness",
            AudienceHint = "Scope and readiness",
            Summary = "Use this when you need to decide which families to crawl, whether a category is mature enough, or how to interpret readiness without over-trusting the numbers.",
            Links =
            [
                new HelpLinkModel { Label = "Open Categories", Page = "/Categories/Index" },
                new HelpLinkModel { Label = "Open Dashboard", Page = "/Index" }
            ],
            Journeys =
            [
                new HelpJourneyModel
                {
                    Title = "Select the right operating scope",
                    Outcome = "You end up with a category set that matches the sources and analysis you actually intend to support.",
                    Steps =
                    [
                        "Use the family grouping to decide whether you are operating in one family or intentionally spanning several.",
                        "Select only the categories you are prepared to seed with sources and review after the crawl.",
                        "Use the average completeness figure in the selection summary as a quick health check for the current set.",
                        "Prefer a smaller but better-understood selection over enabling every family immediately."
                    ]
                },
                new HelpJourneyModel
                {
                    Title = "Interpret readiness signals correctly",
                    Outcome = "You can explain whether a weak category result is caused by the source estate, the schema maturity, or both.",
                    Steps =
                    [
                        "Read the schema-completeness score as a preparedness signal, not as a promise that the crawl output will be high quality.",
                        "Check whether a category is enabled and crawl-supported before expecting it to participate in launch flows.",
                        "If a category is selected but results stay thin, compare source coverage and source readiness next rather than assuming the category definition is at fault.",
                        "If business rules for required attributes have changed, plan a schema update first and then rerun the crawl so the quality surface reflects the revised expectations."
                    ]
                }
            ],
            Questions =
            [
                new HelpQuestionModel
                {
                    Question = "Why is a category visible but not selectable?",
                    Answer = "The category can appear in the family list even when it is not currently enabled or not marked as crawl-supported for the current rollout. The badges on the card tell you whether it is enabled and whether crawl support is available."
                },
                new HelpQuestionModel
                {
                    Question = "Should I crawl several families together?",
                    Answer = "Only if the sources and review capacity support that decision. Multi-family selection is valid, but for first runs it is usually easier to understand one narrower operating scope at a time."
                },
                new HelpQuestionModel
                {
                    Question = "What does a low completeness score usually mean for operators?",
                    Answer = "Expect more interpretation work after the crawl. Missing or weak schema coverage can make quality gaps look larger and can limit how confidently you judge missing attributes or disagreements."
                }
            ]
        },
        new HelpSectionModel
        {
            Id = "sources-and-discovery",
            Title = "Source Onboarding And Discovery",
            AudienceHint = "Build coverage",
            Summary = "Use this when you are adding known sources, exploring new candidate hosts, or deciding whether automation should be conservative or more aggressive.",
            Links =
            [
                new HelpLinkModel { Label = "Source registry", Page = "/Sources/Index" },
                new HelpLinkModel { Label = "Source intelligence", Page = "/Sources/Intelligence" }
            ],
            Journeys =
            [
                new HelpJourneyModel
                {
                    Title = "Register a known source safely",
                    Outcome = "You create a source record that is aligned to the intended market, categories, and onboarding posture before it participates in broader discovery.",
                    Steps =
                    [
                        "Use the Add Source form when you already know the host you want to onboard.",
                        "Set a stable source identifier, readable display name, and correct base URL first because those anchor the rest of the source record.",
                        "Choose allowed markets and category coverage carefully so the source only enters the parts of the workflow it should support.",
                        "Use operator-assisted automation by default unless you have already proven the acceptance thresholds are trustworthy.",
                        "Enable the source immediately only when you are comfortable with its readiness and guardrails."
                    ]
                },
                new HelpJourneyModel
                {
                    Title = "Use candidate discovery to widen the registry",
                    Outcome = "You move from a small known-source estate to a broader shortlist without manually researching every host first.",
                    Steps =
                    [
                        "Choose the categories you want discovery to serve before you search for candidates.",
                        "Provide a locale and market that match the operating region you care about so discovery stays relevant.",
                        "Review candidate reasons and recommendation cues rather than treating the list as an automatic approval queue.",
                        "Promote the strongest candidates into the registration path, then tighten onboarding decisions with visible automation controls.",
                        "Use Source Intelligence later to decide which onboarded sources deserve long-term trust and which need review."
                    ]
                }
            ],
            Questions =
            [
                new HelpQuestionModel
                {
                    Question = "When should I use operator-assisted automation?",
                    Answer = "Use it for new environments, unfamiliar markets, or any source you have not validated before. It keeps the human decision in the loop while still surfacing recommendation signals."
                },
                new HelpQuestionModel
                {
                    Question = "What if candidate discovery returns nothing useful?",
                    Answer = "First check the selected categories, locale, and market. If those are correct, widen the search by onboarding a few known hosts manually so the platform can operate while you revisit candidate discovery later."
                },
                new HelpQuestionModel
                {
                    Question = "What does boot-ready usually mean in practice?",
                    Answer = "It means the source has enough configuration and operational posture to participate in the startup path without additional manual tuning. Enabled and discovery-configured sources are usually the best starting set."
                }
            ]
        },
        new HelpSectionModel
        {
            Id = "crawl-launch-and-monitoring",
            Title = "Crawl Launch And Monitoring",
            AudienceHint = "Run the pipeline",
            Summary = "Use this when the categories and sources exist and you need to launch work, interpret queue movement, and decide whether the crawl is behaving normally.",
            Links =
            [
                new HelpLinkModel { Label = "Launch from dashboard", Page = "/Index" },
                new HelpLinkModel { Label = "Crawl jobs", Page = "/CrawlJobs/Index" }
            ],
            Journeys =
            [
                new HelpJourneyModel
                {
                    Title = "Launch a seeded category crawl",
                    Outcome = "You begin a crawl from a deliberate category scope rather than from a vague source list, which makes downstream monitoring easier to interpret.",
                    Steps =
                    [
                        "Launch from Categories when you want the selection step and launch decision in one place.",
                        "Launch from the Dashboard when you already trust the current category context and want to move quickly.",
                        "Treat the initial job as a validation run for source posture and discovery behaviour, not only as a product-harvesting exercise.",
                        "Watch for confirmed products and canonical product counts to start moving after discovery has had time to expand the frontier."
                    ]
                },
                new HelpJourneyModel
                {
                    Title = "Read queue posture without overreacting",
                    Outcome = "You can distinguish healthy queue build-up from signs that the crawl is stalling or sourcing poor candidates.",
                    Steps =
                    [
                        "Use the Dashboard for high-level pressure signals such as queue depth, retry backlog, and category hotspots.",
                        "Use Crawl Jobs for job-level detail when you need to understand whether discovery, product confirmation, or downstream crawl work is lagging.",
                        "A growing discovery queue is not automatically bad if confirmed products and canonical products begin to follow.",
                        "If retries or failures dominate the movement, switch to Source Intelligence and source detail views to find the weak hosts."
                    ]
                }
            ],
            Questions =
            [
                new HelpQuestionModel
                {
                    Question = "What should I look at first after launch?",
                    Answer = "Start with queue depth, confirmed product targets, failures, and recent product throughput. Those four signals tell you whether discovery is feeding the crawl and whether the crawl is producing usable catalogue output."
                },
                new HelpQuestionModel
                {
                    Question = "The queue is growing but products are not. What is the likely next check?",
                    Answer = "Inspect source readiness and source quality next. A large frontier with weak confirmation usually means the sources, discovery rules, or candidate quality need attention before the product pipeline can keep up."
                },
                new HelpQuestionModel
                {
                    Question = "When do retries become a real concern?",
                    Answer = "When retry backlog persists and begins to crowd out successful progress across the same sources or categories. That is the point where Source Intelligence and source-level review become the fastest path to stabilisation."
                }
            ]
        },
        new HelpSectionModel
        {
            Id = "products-and-quality",
            Title = "Products, Conflicts, And Quality Review",
            AudienceHint = "Judge the output",
            Summary = "Use this when crawl work has produced catalogue data and you need to verify whether the merged product view is trustworthy, complete, and stable enough to act on.",
            Links =
            [
                new HelpLinkModel { Label = "Products", Page = "/Products/Index" },
                new HelpLinkModel { Label = "Quality", Page = "/Quality/Index" }
            ],
            Journeys =
            [
                new HelpJourneyModel
                {
                    Title = "Inspect a canonical product end to end",
                    Outcome = "You can explain what the product currently claims, which sources support that view, and where the weak points are.",
                    Steps =
                    [
                        "Open Products when you want to filter the catalogue and find representative records to inspect.",
                        "Open an individual product detail page to compare source evidence, inspect key attributes, and review the merge result in context.",
                        "Use the conflict and evidence sections to understand where sources disagree instead of assuming the merged value is always obvious.",
                        "Use the timeline and history views when a product appears unstable or when you need to explain why a value changed."
                    ]
                },
                new HelpJourneyModel
                {
                    Title = "Turn quality findings into operator action",
                    Outcome = "You can decide whether the next action belongs in category readiness, source onboarding, crawl operations, or product review.",
                    Steps =
                    [
                        "Use the Quality dashboard for category-level issues such as low coverage, unmapped attributes, and disagreement hotspots.",
                        "If quality weakness is broad across a category, revisit category readiness and source coverage first.",
                        "If one source dominates the weak signals, move into Source Intelligence rather than trying to solve the issue only from product detail pages.",
                        "If a handful of products are problematic while the rest of the category looks healthy, stay in the product detail workflow and review conflicts, evidence, and timelines there."
                    ]
                }
            ],
            Questions =
            [
                new HelpQuestionModel
                {
                    Question = "Where do I investigate disagreements between sources?",
                    Answer = "Start on the product detail page where the conflict and evidence views make the disagreement concrete, then move out to Quality or Source Intelligence if the same pattern appears across many products."
                },
                new HelpQuestionModel
                {
                    Question = "Why are attributes missing on products that clearly exist?",
                    Answer = "Common reasons are weak schema coverage, low extractability on one or more sources, or source pages that omit the attribute entirely. Use Quality for the broad pattern and product detail for a specific record."
                },
                new HelpQuestionModel
                {
                    Question = "When should I use the Quality page instead of Products?",
                    Answer = "Use Products to inspect individual records and Quality to understand whether the problem is systemic across a category, attribute family, or source estate."
                }
            ]
        },
        new HelpSectionModel
        {
            Id = "operations-and-troubleshooting",
            Title = "Ongoing Operations And Troubleshooting",
            AudienceHint = "Keep it healthy",
            Summary = "Use this when the system is already live and you need a routine for watchlists, weak sources, category hotspots, and the places to start when quality or throughput drops.",
            Links =
            [
                new HelpLinkModel { Label = "Source intelligence", Page = "/Sources/Intelligence" },
                new HelpLinkModel { Label = "Dashboard", Page = "/Index" },
                new HelpLinkModel { Label = "Quality", Page = "/Quality/Index" }
            ],
            Journeys =
            [
                new HelpJourneyModel
                {
                    Title = "Run a simple daily operator rhythm",
                    Outcome = "You cover the main operational risks without bouncing randomly between pages.",
                    Steps =
                    [
                        "Start on the Dashboard for queue pressure, failure posture, and category concentration.",
                        "Move to Crawl Jobs only when the top-level metrics suggest a specific job or run needs closer inspection.",
                        "Use Source Intelligence to separate weak-source problems from category-wide demand or schema issues.",
                        "Finish in Quality or Products depending on whether the impact looks systemic or record-specific."
                    ]
                },
                new HelpJourneyModel
                {
                    Title = "Triage a quality or throughput regression",
                    Outcome = "You identify whether the regression is mostly about source trust, extraction quality, category pressure, or crawl backlog.",
                    Steps =
                    [
                        "Confirm on the Dashboard whether the issue shows up as backlog, failure, or category pressure first.",
                        "Use Source Intelligence to identify weak or drifting sources and save recurring review queues when a pattern repeats.",
                        "Use the Quality page to see whether coverage or disagreements have deteriorated across the category.",
                        "Drop into product detail only after you know the regression is concentrated in a narrower slice of output."
                    ]
                }
            ],
            Questions =
            [
                new HelpQuestionModel
                {
                    Question = "Where should I start if stakeholders say quality has dropped?",
                    Answer = "Start with Dashboard and Quality together. The Dashboard tells you whether runtime posture changed, while Quality tells you whether the catalogue output itself changed across coverage, disagreements, or stability."
                },
                new HelpQuestionModel
                {
                    Question = "What is Source Intelligence best at answering?",
                    Answer = "It is the best place to compare sources over time, identify weak or high-value hosts, and save repeatable review queues for ongoing analyst work."
                },
                new HelpQuestionModel
                {
                    Question = "How do I know whether a problem is category-wide or source-specific?",
                    Answer = "If the quality or throughput issue follows one source strongly, Source Intelligence will usually expose it quickly. If the problem appears across several sources in the same category, look at category readiness, category pressure, or broader crawl load instead."
                }
            ]
        }
    ];

    public void OnGet()
    {
    }
}

public sealed class HelpSectionModel
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string AudienceHint { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<HelpLinkModel> Links { get; init; } = [];
    public IReadOnlyList<HelpJourneyModel> Journeys { get; init; } = [];
    public IReadOnlyList<HelpQuestionModel> Questions { get; init; } = [];
}

public sealed class HelpLinkModel
{
    public string Label { get; init; } = string.Empty;
    public string Page { get; init; } = string.Empty;
}

public sealed class HelpJourneyModel
{
    public string Title { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public IReadOnlyList<string> Steps { get; init; } = [];
}

public sealed class HelpQuestionModel
{
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
}