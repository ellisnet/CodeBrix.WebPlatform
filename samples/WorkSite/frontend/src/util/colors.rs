//! The client colours from the API contract and the Tailwind classes for each. Every class is
//! written out in full so Tailwind's scanner finds it; never build these names at run time.

/// The colour names the API accepts, in the order the picker shows them.
pub const CLIENT_COLORS: [&str; 10] = [
    "slate", "red", "orange", "amber", "green", "teal", "sky", "indigo", "violet", "pink",
];

/// The colour a new client starts with.
pub const DEFAULT_CLIENT_COLOR: &str = "indigo";

pub fn color_label(color: &str) -> &'static str {
    match color {
        "red" => "Red",
        "orange" => "Orange",
        "amber" => "Amber",
        "green" => "Green",
        "teal" => "Teal",
        "sky" => "Sky",
        "indigo" => "Indigo",
        "violet" => "Violet",
        "pink" => "Pink",
        _ => "Slate",
    }
}

/// Background, text and ring classes for a client badge.
pub fn badge_classes(color: &str) -> &'static str {
    match color {
        "red" => "bg-red-50 text-red-700 ring-red-600/20",
        "orange" => "bg-orange-50 text-orange-700 ring-orange-600/20",
        "amber" => "bg-amber-50 text-amber-800 ring-amber-600/25",
        "green" => "bg-green-50 text-green-700 ring-green-600/20",
        "teal" => "bg-teal-50 text-teal-700 ring-teal-600/20",
        "sky" => "bg-sky-50 text-sky-700 ring-sky-600/20",
        "indigo" => "bg-indigo-50 text-indigo-700 ring-indigo-600/20",
        "violet" => "bg-violet-50 text-violet-700 ring-violet-600/20",
        "pink" => "bg-pink-50 text-pink-700 ring-pink-600/20",
        _ => "bg-slate-100 text-slate-700 ring-slate-500/20",
    }
}

/// A solid background for dots and swatches.
pub fn dot_class(color: &str) -> &'static str {
    match color {
        "red" => "bg-red-500",
        "orange" => "bg-orange-500",
        "amber" => "bg-amber-500",
        "green" => "bg-green-500",
        "teal" => "bg-teal-500",
        "sky" => "bg-sky-500",
        "indigo" => "bg-indigo-500",
        "violet" => "bg-violet-500",
        "pink" => "bg-pink-500",
        _ => "bg-slate-500",
    }
}

/// An SVG fill for chart bars.
pub fn fill_class(color: &str) -> &'static str {
    match color {
        "red" => "fill-red-500",
        "orange" => "fill-orange-500",
        "amber" => "fill-amber-500",
        "green" => "fill-green-500",
        "teal" => "fill-teal-500",
        "sky" => "fill-sky-500",
        "indigo" => "fill-indigo-500",
        "violet" => "fill-violet-500",
        "pink" => "fill-pink-500",
        "" => "fill-indigo-500",
        _ => "fill-slate-500",
    }
}

/// The ring drawn round the selected swatch in the colour picker.
pub fn selected_ring_class(color: &str) -> &'static str {
    match color {
        "red" => "ring-red-500",
        "orange" => "ring-orange-500",
        "amber" => "ring-amber-500",
        "green" => "ring-green-500",
        "teal" => "ring-teal-500",
        "sky" => "ring-sky-500",
        "indigo" => "ring-indigo-500",
        "violet" => "ring-violet-500",
        "pink" => "ring-pink-500",
        _ => "ring-slate-500",
    }
}

/// Returns `color` when it is one the API knows, otherwise the default.
pub fn normalise(color: &str) -> &'static str {
    CLIENT_COLORS
        .iter()
        .copied()
        .find(|c| *c == color)
        .unwrap_or(DEFAULT_CLIENT_COLOR)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn every_contract_colour_has_its_own_classes() {
        for color in CLIENT_COLORS {
            assert!(
                badge_classes(color).split(' ').any(|c| c.starts_with("bg-") && c.contains(color)),
                "{color}"
            );
            assert!(dot_class(color).contains(color), "{color}");
            assert!(fill_class(color).contains(color), "{color}");
            assert!(selected_ring_class(color).contains(color), "{color}");
            assert_eq!(color_label(color).to_lowercase(), color);
        }
    }

    #[test]
    fn unknown_colours_fall_back() {
        assert_eq!(badge_classes("chartreuse"), badge_classes("slate"));
        assert_eq!(normalise("chartreuse"), DEFAULT_CLIENT_COLOR);
        assert_eq!(normalise("teal"), "teal");
        assert_eq!(fill_class(""), "fill-indigo-500");
    }
}
