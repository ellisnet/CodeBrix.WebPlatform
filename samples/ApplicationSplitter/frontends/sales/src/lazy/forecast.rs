//! The revenue forecast: a deliberately heavy calculation (thousands of simulated years) behind
//! `#[wasm_split(forecast)]`.
//!
//! The attribute turns `run_forecast` into a small stub in the reports page's code; the body,
//! and everything only it uses (the simulation and the random numbers), goes into a piece of
//! WebAssembly called `forecast`. The first call downloads the piece and then runs the body;
//! later calls run it at once. The function must be `async` for that reason: calling it may
//! have to wait for the download.

use dioxus::wasm_split;

use crate::data::MONTHLY_REVENUE;

/// What the forecast says about one coming month, in whole currency units.
#[derive(Clone, Debug, PartialEq)]
pub struct ForecastMonth {
    /// "Dec", "Jan", ...
    pub month: &'static str,
    /// Nine runs in ten came out above this.
    pub low: i64,
    /// The middle run.
    pub expected: i64,
    /// Nine runs in ten came out below this.
    pub high: i64,
}

/// The forecast for the next twelve months.
#[derive(Clone, Debug, PartialEq)]
pub struct Forecast {
    pub months: Vec<ForecastMonth>,
    /// How many simulated years it is based on.
    pub runs: u32,
    /// The expected revenue of the whole year.
    pub expected_year: i64,
}

/// Simulates `runs` possible next years from the last twelve months and summarises them. The
/// first call downloads the `forecast` piece.
pub async fn forecast(runs: u32, seed: u64) -> Forecast {
    run_forecast(runs, seed).await
}

/// The split function itself. (The attribute drops a function's `pub` in the WebAssembly build,
/// so the page calls the public `forecast` above, which stays in the page's own code.)
#[wasm_split::wasm_split(forecast)]
async fn run_forecast(runs: u32, seed: u64) -> Forecast {
    simulate(runs, seed)
}

/// A small, fast random number generator (xorshift64*), so the forecast needs no crate.
struct Random(u64);

impl Random {
    fn next(&mut self) -> u64 {
        let mut x = self.0;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        self.0 = x;
        x.wrapping_mul(0x2545_F491_4F6C_DD1D)
    }

    /// A number from a normal distribution with mean 0 and spread 1 (Irwin-Hall approximation).
    fn normal(&mut self) -> f64 {
        let sum: f64 = (0..12).map(|_| (self.next() >> 11) as f64 / (1u64 << 53) as f64).sum();
        sum - 6.0
    }
}

fn simulate(runs: u32, seed: u64) -> Forecast {
    let history: Vec<f64> = MONTHLY_REVENUE.iter().map(|m| m.revenue as f64).collect();
    let growth: f64 = history.windows(2).map(|w| (w[1] / w[0]).ln()).sum::<f64>() / (history.len() - 1) as f64;
    let mean_growth = growth.clamp(-0.05, 0.05);
    let spread = 0.06;
    let mut random = Random(seed | 1);
    let runs = runs.max(10);
    // outcomes[month] holds every run's revenue for that month.
    let mut outcomes: Vec<Vec<f64>> = vec![Vec::with_capacity(runs as usize); 12];
    for _ in 0..runs {
        let mut level = history[history.len() - 1];
        for (month, outcome) in outcomes.iter_mut().enumerate() {
            // Revenue follows last year's season shape, with a random walk around the trend.
            let season = history[month] / history.iter().sum::<f64>() * 12.0;
            level *= (mean_growth + spread * random.normal()).exp();
            outcome.push(level * (0.7 + 0.3 * season));
        }
    }
    let mut months = Vec::with_capacity(12);
    let mut expected_year = 0;
    for (month, outcome) in outcomes.iter_mut().enumerate() {
        outcome.sort_by(|a, b| a.total_cmp(b));
        let at = |share: f64| outcome[((outcome.len() - 1) as f64 * share) as usize].round() as i64;
        let expected = at(0.5);
        expected_year += expected;
        months.push(ForecastMonth { month: MONTHLY_REVENUE[month].month, low: at(0.1), expected, high: at(0.9) });
    }
    Forecast { months, runs, expected_year }
}
