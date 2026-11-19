//! Reloading data that changed somewhere else (another tab, the running timer): `ChangesHub`
//! sends a `Changed` message for every client, project or time entry the user's account
//! creates, updates or deletes, and the pages showing data built from that kind of item load it
//! again.
//!
//! The app keeps one counter per kind of item ([`ChangeStamps`]). Each list or summary a page
//! loads names the kinds it is built from (the constants below) and reads their combined stamp
//! inside its `use_resource`, so the resource runs again exactly when one of them changes.

use std::rc::Rc;

use codebrix_webplatform::signalr::Subscription;
use dioxus::prelude::*;

use crate::api::models::{ChangedEntity, DataChange};
use crate::generated::hubs::ChangesHub;

/// Clients with their project counts and hours (the clients page).
pub const CLIENTS_WITH_TOTALS: &[ChangedEntity] = &[ChangedEntity::Client, ChangedEntity::Project, ChangedEntity::TimeEntry];
/// The clients a filter or a form offers.
pub const CLIENT_CHOICES: &[ChangedEntity] = &[ChangedEntity::Client];
/// Projects with their client's name and their hours (the projects page).
pub const PROJECTS_WITH_TOTALS: &[ChangedEntity] = &[ChangedEntity::Project, ChangedEntity::Client, ChangedEntity::TimeEntry];
/// The projects a filter or a form offers, named with their client (archiving a client also
/// takes its projects off the active list).
pub const PROJECT_CHOICES: &[ChangedEntity] = &[ChangedEntity::Project, ChangedEntity::Client];
/// Time entries, which show their project's and their client's name (the time log).
pub const TIME_ENTRIES: &[ChangedEntity] = &[ChangedEntity::TimeEntry, ChangedEntity::Project, ChangedEntity::Client];
/// Totals worked out from the entries, by client and project (the dashboard, the reports).
pub const SUMMARIES: &[ChangedEntity] = &[ChangedEntity::TimeEntry, ChangedEntity::Project, ChangedEntity::Client];

/// What the changes hub reported.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ChangeNote {
    /// One item of this kind changed.
    One(ChangedEntity),
    /// The connection was lost for a while, so changes may have been missed: anything may be stale.
    All,
}

/// How many changes of each kind this page has heard of.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct ChangeStamps {
    clients: u64,
    projects: u64,
    time_entries: u64,
}

impl ChangeStamps {
    pub fn apply(&mut self, note: ChangeNote) {
        match note {
            ChangeNote::One(entity) => self.record(entity),
            ChangeNote::All => {
                for entity in ChangedEntity::ALL {
                    self.record(entity);
                }
            }
        }
    }

    fn record(&mut self, entity: ChangedEntity) {
        let counter = match entity {
            ChangedEntity::Client => &mut self.clients,
            ChangedEntity::Project => &mut self.projects,
            ChangedEntity::TimeEntry => &mut self.time_entries,
        };
        *counter = counter.wrapping_add(1);
    }

    /// A number that changes whenever an item of one of these kinds changes.
    pub fn stamp(&self, built_from: &[ChangedEntity]) -> u64 {
        ChangedEntity::ALL
            .iter()
            .filter(|entity| built_from.contains(entity))
            .map(|entity| match entity {
                ChangedEntity::Client => self.clients,
                ChangedEntity::Project => self.projects,
                ChangedEntity::TimeEntry => self.time_entries,
            })
            .fold(0u64, u64::wrapping_add)
    }
}

pub static CHANGE_STAMPS: GlobalSignal<ChangeStamps> = Signal::global(ChangeStamps::default);

/// Records a change; the pages showing data built from it load it again.
pub fn note(change: ChangeNote) {
    CHANGE_STAMPS.write().apply(change);
}

/// A hook: the combined stamp of `built_from`, for a `use_resource` to read so that it runs again
/// when data of one of those kinds changes.
pub fn use_change_stamp(built_from: &'static [ChangedEntity]) -> Memo<u64> {
    use_memo(move || CHANGE_STAMPS.read().stamp(built_from))
}

/// Passes every `Changed` message, and every reconnect, to `on_note` until the subscriptions are
/// dropped.
pub fn follow(hub: &ChangesHub, on_note: impl Fn(ChangeNote) + 'static) -> [Subscription; 2] {
    let on_note = Rc::new(on_note);
    let on_change = on_note.clone();
    [
        hub.on_changed(move |change: DataChange| on_change(ChangeNote::One(change.entity))),
        hub.connection().on_reconnected(move || on_note(ChangeNote::All)),
    ]
}

#[cfg(test)]
mod tests {
    use std::cell::RefCell;

    use codebrix_webplatform::signalr::testing::{settle, FakeHub};
    use serde_json::json;

    use super::*;
    use crate::generated::hubs::CHANGES_HUB;

    fn after(notes: &[ChangeNote]) -> ChangeStamps {
        let mut stamps = ChangeStamps::default();
        for note in notes {
            stamps.apply(*note);
        }
        stamps
    }

    #[test]
    fn a_new_time_entry_reloads_entries_and_totals_but_not_the_pickers() {
        let before = ChangeStamps::default();
        let now = after(&[ChangeNote::One(ChangedEntity::TimeEntry)]);
        for reloads in [TIME_ENTRIES, SUMMARIES, CLIENTS_WITH_TOTALS, PROJECTS_WITH_TOTALS] {
            assert_ne!(before.stamp(reloads), now.stamp(reloads));
        }
        for stays in [CLIENT_CHOICES, PROJECT_CHOICES] {
            assert_eq!(before.stamp(stays), now.stamp(stays));
        }
    }

    #[test]
    fn a_client_change_reloads_everything_that_shows_client_names() {
        let before = ChangeStamps::default();
        let now = after(&[ChangeNote::One(ChangedEntity::Client)]);
        for reloads in [CLIENT_CHOICES, PROJECT_CHOICES, TIME_ENTRIES, SUMMARIES, CLIENTS_WITH_TOTALS, PROJECTS_WITH_TOTALS] {
            assert_ne!(before.stamp(reloads), now.stamp(reloads));
        }
    }

    #[test]
    fn a_project_change_leaves_the_client_choices_alone() {
        let before = ChangeStamps::default();
        let now = after(&[ChangeNote::One(ChangedEntity::Project)]);
        assert_eq!(before.stamp(CLIENT_CHOICES), now.stamp(CLIENT_CHOICES));
        assert_ne!(before.stamp(PROJECT_CHOICES), now.stamp(PROJECT_CHOICES));
        assert_ne!(before.stamp(CLIENTS_WITH_TOTALS), now.stamp(CLIENTS_WITH_TOTALS));
    }

    #[test]
    fn every_change_moves_the_stamp_even_when_kinds_alternate() {
        let one = after(&[ChangeNote::One(ChangedEntity::Client)]);
        let two = after(&[ChangeNote::One(ChangedEntity::Client), ChangeNote::One(ChangedEntity::Project)]);
        assert_ne!(one.stamp(PROJECT_CHOICES), two.stamp(PROJECT_CHOICES));
    }

    #[test]
    fn a_reconnect_reloads_everything() {
        let before = ChangeStamps::default();
        let now = after(&[ChangeNote::All]);
        for kinds in [CLIENT_CHOICES, PROJECT_CHOICES, TIME_ENTRIES] {
            assert_ne!(before.stamp(kinds), now.stamp(kinds));
        }
    }

    #[test]
    fn changed_messages_and_reconnects_reach_the_stamps() {
        let fake = FakeHub::install(&CHANGES_HUB);
        let hub = ChangesHub::connect();
        let stamps = Rc::new(RefCell::new(ChangeStamps::default()));
        let sink = stamps.clone();
        let _subscriptions = follow(&hub, move |note| sink.borrow_mut().apply(note));
        settle();

        fake.push("Changed", vec![json!({"entity": "TimeEntry", "kind": "Created", "id": "t1"})]);
        assert_eq!(*stamps.borrow(), after(&[ChangeNote::One(ChangedEntity::TimeEntry)]));

        fake.push("Changed", vec![json!({"entity": "Nonsense", "kind": "Created", "id": "x"})]);
        assert_eq!(*stamps.borrow(), after(&[ChangeNote::One(ChangedEntity::TimeEntry)]));

        fake.drop_connection();
        assert_eq!(
            *stamps.borrow(),
            after(&[ChangeNote::One(ChangedEntity::TimeEntry), ChangeNote::All])
        );
    }
}
