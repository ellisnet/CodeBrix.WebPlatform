//! A self-check of text formatting in the split build.
//!
//! Bundle splitting takes, out of the main WebAssembly file, the code AND THE DATA that only a
//! piece uses; a piece brings its data back when it is downloaded. The data the main file itself
//! needs must stay whole. The constants most easily damaged are the smallest ones: the byte
//! template the compiler stores for each `format!`, which the linker may keep as the tail of a
//! longer constant that belongs to a piece. A template whose bytes are gone formats to nothing.
//!
//! The probes below run in the main file (the layout calls them, and the router never splits a
//! layout). Each formats known values and is compared with the text it must give. They run once
//! when the application starts, before any piece has arrived (`at_start`), and again whenever
//! the diagnostics page is drawn (`run`), so a difference between the two shows data that a
//! piece brought back. The diagnostics page (`pages::diagnostics`) has probes of its own, which
//! live in that page's piece.

use std::hint::black_box;
use std::sync::OnceLock;

/// One formatting expression, the text it must give and the text it gave.
#[derive(Clone, Debug, PartialEq)]
pub struct Probe {
    /// The expression, as it is written in the source.
    pub expression: &'static str,
    /// The text the expression must give.
    pub expected: &'static str,
    /// The text it gave.
    pub actual: String,
}

impl Probe {
    /// Whether the expression gave the text it must give.
    pub fn passed(&self) -> bool {
        self.actual == self.expected
    }
}

/// How many of the probes did not give the text they must give.
pub fn failures(probes: &[Probe]) -> usize {
    probes.iter().filter(|p| !p.passed()).count()
}

/// A text between double quotes, put together without `format!` so that it shows what a probe
/// gave even when formatting itself is what is broken. An empty text shows as `""`.
pub fn quoted(text: &str) -> String {
    let mut out = String::with_capacity(text.len() + 2);
    out.push('"');
    out.push_str(text);
    out.push('"');
    out
}

/// Runs the probes of the main file now. The values go through `black_box` so that the compiler
/// formats them when the application runs, not when it is built.
#[inline(never)]
pub fn run() -> Vec<Probe> {
    let a = black_box(String::from("ab"));
    let b = black_box("/cd");
    let c = black_box("ef");
    let n = black_box(42_i32);
    let mut pushed = a.clone();
    pushed.push_str(b);
    vec![
        // The form the platform's REST client builds every address with (origin, then path).
        Probe { expression: r#"format!("{}{}", a, b)"#, expected: "ab/cd", actual: format!("{}{}", a, b) },
        Probe { expression: r#"format!("{}", a)"#, expected: "ab", actual: format!("{}", a) },
        Probe { expression: r#"format!("{}{}{}", a, b, c)"#, expected: "ab/cdef", actual: format!("{}{}{}", a, b, c) },
        Probe { expression: r#"format!("{}|{}", a, b)"#, expected: "ab|/cd", actual: format!("{}|{}", a, b) },
        Probe { expression: r#"format!("{} {}", a, b)"#, expected: "ab /cd", actual: format!("{} {}", a, b) },
        Probe { expression: "n.to_string()", expected: "42", actual: n.to_string() },
        // No template at all: the comparison that must always hold.
        Probe { expression: "a.clone() + push_str(b)", expected: "ab/cd", actual: pushed },
    ]
}

static AT_START: OnceLock<Vec<Probe>> = OnceLock::new();

/// What the probes gave the first time this was called. `App` calls it on its first render, so
/// that is the state of the main file before any piece has been downloaded.
pub fn at_start() -> &'static [Probe] {
    AT_START.get_or_init(run)
}
