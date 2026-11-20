//! Made-up figures that live in the frontend (the backend serves the summary, the orders and the
//! customers): the revenue of the last twelve months, the product catalogue, the pipeline and
//! the team. Only enough to fill the pages.

/// One month of revenue, in whole currency units.
pub struct MonthRevenue {
    pub month: &'static str,
    pub revenue: i64,
}

/// The last twelve months, oldest first.
pub const MONTHLY_REVENUE: [MonthRevenue; 12] = [
    MonthRevenue { month: "Dec", revenue: 121_400 },
    MonthRevenue { month: "Jan", revenue: 98_300 },
    MonthRevenue { month: "Feb", revenue: 104_900 },
    MonthRevenue { month: "Mar", revenue: 132_750 },
    MonthRevenue { month: "Apr", revenue: 128_100 },
    MonthRevenue { month: "May", revenue: 141_600 },
    MonthRevenue { month: "Jun", revenue: 137_200 },
    MonthRevenue { month: "Jul", revenue: 149_800 },
    MonthRevenue { month: "Aug", revenue: 126_450 },
    MonthRevenue { month: "Sep", revenue: 158_300 },
    MonthRevenue { month: "Oct", revenue: 171_900 },
    MonthRevenue { month: "Nov", revenue: 184_250 },
];

/// One product of the catalogue.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Product {
    pub sku: &'static str,
    pub name: &'static str,
    pub unit_price: i64,
    /// Share of this year's revenue, in percent.
    pub share: u32,
}

/// What the team sells.
pub const PRODUCTS: [Product; 5] = [
    Product { sku: "AS-100", name: "Analytics Suite", unit_price: 4_800, share: 38 },
    Product { sku: "FL-220", name: "Fleet Licences (10 seats)", unit_price: 2_150, share: 24 },
    Product { sku: "SP-310", name: "Support Plan, Premium", unit_price: 1_200, share: 17 },
    Product { sku: "ON-050", name: "On-site Onboarding", unit_price: 3_400, share: 13 },
    Product { sku: "TR-075", name: "Team Training Day", unit_price: 950, share: 8 },
];

/// One stage of the sales pipeline.
pub struct PipelineStage {
    pub stage: &'static str,
    pub deals: u32,
    pub value: i64,
    /// The chance a deal in this stage is won, in percent.
    pub win_chance: u32,
}

/// The pipeline today.
pub const PIPELINE: [PipelineStage; 5] = [
    PipelineStage { stage: "Qualified", deals: 42, value: 612_000, win_chance: 10 },
    PipelineStage { stage: "Discovery", deals: 27, value: 455_500, win_chance: 25 },
    PipelineStage { stage: "Proposal sent", deals: 18, value: 338_200, win_chance: 45 },
    PipelineStage { stage: "Negotiation", deals: 9, value: 214_900, win_chance: 70 },
    PipelineStage { stage: "Verbal yes", deals: 4, value: 96_400, win_chance: 90 },
];

/// One member of the sales team and the quarter so far.
pub struct Rep {
    pub name: &'static str,
    pub region: &'static str,
    pub closed: i64,
    pub target: i64,
}

/// The team.
pub const REPS: [Rep; 5] = [
    Rep { name: "Priya Raman", region: "Europe", closed: 182_400, target: 200_000 },
    Rep { name: "Mateo Alvarez", region: "South America", closed: 141_250, target: 160_000 },
    Rep { name: "Kenji Mori", region: "Asia Pacific", closed: 167_900, target: 150_000 },
    Rep { name: "Olivia Brandt", region: "North America", closed: 118_600, target: 175_000 },
    Rep { name: "Amara Diallo", region: "Africa", closed: 74_300, target: 90_000 },
];
