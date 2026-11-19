//! What this application adds to the generated contract types: a user's full name, the page
//! count of a page of time entries, the request bodies that only flip an archived flag, the time
//! log's paging limits and the report page's filter. Pure logic, tested on the host.

use codebrix_webplatform::dates::Date;

use crate::api::models::{Client, ClientRequest, Project, ProjectRequest, TimeEntryPage, UserResponse};
use crate::api::reports::{self, ExportQuery, SummaryQuery};
use crate::api::time_entries::ListQuery;
use crate::util::report::GroupBy;

/// The most time entries one page of the time log asks for (the backend's limit).
pub const MAX_PAGE_SIZE: i32 = 100;

/// A user's name as one line.
pub trait FullName {
    /// "Ada Lovelace", or just the part that is there.
    fn full_name(&self) -> String;
}

impl FullName for UserResponse {
    fn full_name(&self) -> String {
        format!("{} {}", self.first_name, self.last_name).trim().to_string()
    }
}

/// How many pages a list of time entries has.
pub trait PageCount {
    /// At least 1, even for an empty list.
    fn page_count(&self) -> i32;
}

impl PageCount for TimeEntryPage {
    fn page_count(&self) -> i32 {
        if self.page_size <= 0 {
            1
        } else {
            ((self.total_count + self.page_size - 1) / self.page_size).max(1)
        }
    }
}

/// The body of a `PUT` that keeps everything about an item but its archived flag.
pub trait ArchiveToggle<T> {
    fn archive_toggle(item: &T, archived: bool) -> Self;
}

impl ArchiveToggle<Client> for ClientRequest {
    fn archive_toggle(client: &Client, archived: bool) -> ClientRequest {
        ClientRequest {
            name: client.name.clone(),
            contact_name: client.contact_name.clone(),
            contact_email: client.contact_email.clone(),
            notes: client.notes.clone(),
            color: client.color.clone(),
            is_archived: Some(archived),
        }
    }
}

impl ArchiveToggle<Project> for ProjectRequest {
    fn archive_toggle(project: &Project, archived: bool) -> ProjectRequest {
        ProjectRequest {
            client_id: project.client_id.clone(),
            name: project.name.clone(),
            description: project.description.clone(),
            hourly_rate: project.hourly_rate,
            is_billable: project.is_billable,
            is_archived: Some(archived),
        }
    }
}

/// Paging for the time log's query.
pub trait Paged {
    /// The same query for page `page` (at least 1) of `page_size` entries (1 to 100).
    fn paged(self, page: i32, page_size: i32) -> Self;
}

impl Paged for ListQuery {
    fn paged(self, page: i32, page_size: i32) -> ListQuery {
        ListQuery {
            page: Some(page.max(1)),
            page_size: Some(page_size.clamp(1, MAX_PAGE_SIZE)),
            ..self
        }
    }
}

/// The settings of the reports page: the summary's query and the CSV export's address.
#[derive(Clone, Debug, PartialEq)]
pub struct ReportFilter {
    pub from: Date,
    pub to: Date,
    pub group_by: GroupBy,
    pub client_id: Option<String>,
    pub project_id: Option<String>,
}

impl ReportFilter {
    /// The query of `GET /api/reports/summary`.
    pub fn summary_query(&self) -> SummaryQuery {
        SummaryQuery {
            from: Some(self.from),
            to: Some(self.to),
            group_by: Some(self.group_by.key().to_string()),
            client_id: self.client_id.clone(),
            project_id: self.project_id.clone(),
        }
    }

    /// The same-origin link the browser follows to download the CSV (cookies go with it).
    pub fn export_href(&self) -> String {
        reports::export_url(&ExportQuery {
            from: Some(self.from),
            to: Some(self.to),
            client_id: self.client_id.clone(),
            project_id: self.project_id.clone(),
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::api::models::{
        ChangePasswordRequest, Dashboard, MetaResponse, ReportSummary, TimeEntryRequest,
    };

    #[test]
    fn user_reads_camel_case_and_has_a_full_name() {
        let json = r#"{"id":"u1","username":"ada","email":"ada@example.com","firstName":"Ada","lastName":"Lovelace","createdAt":"2026-01-02T03:04:05Z"}"#;
        let user: UserResponse = serde_json::from_str(json).unwrap();
        assert_eq!(user.first_name, "Ada");
        assert_eq!(user.full_name(), "Ada Lovelace");
        let user = UserResponse { first_name: "Ada".into(), ..Default::default() };
        assert_eq!(user.full_name(), "Ada");
    }

    #[test]
    fn client_reads_counts() {
        let json = r#"{"id":"c1","name":"Acme","contactName":"","contactEmail":"","notes":"n","color":"teal","isArchived":false,"createdAt":"2026-01-01T00:00:00Z","projectCount":3,"totalHours":12.5}"#;
        let client: Client = serde_json::from_str(json).unwrap();
        assert_eq!(client.contact_name, "");
        assert_eq!(client.project_count, 3);
        assert_eq!(client.total_hours, 12.5);
    }

    #[test]
    fn project_reads_a_null_rate() {
        let json = r#"{"id":"p1","clientId":"c1","clientName":"Acme","name":"Site","description":"","hourlyRate":null,"isBillable":true,"isArchived":false,"createdAt":"x","totalHours":0}"#;
        let project: Project = serde_json::from_str(json).unwrap();
        assert_eq!(project.hourly_rate, None);
    }

    #[test]
    fn time_entry_page_reads_and_counts_pages() {
        let json = r#"{"items":[{"id":"t1","projectId":"p1","projectName":"Site","clientId":"c1","clientName":"Acme","workDate":"2026-11-17","hours":1.5,"description":"","isBillable":true,"createdAt":"a","updatedAt":"b"}],"page":1,"pageSize":25,"totalCount":51,"totalHours":80.25}"#;
        let page: TimeEntryPage = serde_json::from_str(json).unwrap();
        assert_eq!(page.items.len(), 1);
        assert_eq!(page.items[0].work_date, Date::parse("2026-11-17").unwrap());
        assert_eq!(page.page_count(), 3);
        assert_eq!(TimeEntryPage { page_size: 0, ..page.clone() }.page_count(), 1);
        assert_eq!(TimeEntryPage { total_count: 0, ..page }.page_count(), 1);
    }

    #[test]
    fn dashboard_and_report_read() {
        let dashboard = r#"{"hoursToday":1,"hoursThisWeek":2,"hoursLastWeek":3,"hoursThisMonth":4,"billableHoursThisMonth":3.5,"amountThisMonth":350.0,"activeProjectCount":2,"activeClientCount":1,"dailyHours":[{"date":"2026-11-17","hours":1,"billableHours":1}],"topProjects":[{"projectId":"p","projectName":"P","clientName":"C","color":"sky","hours":4}],"recentEntries":[]}"#;
        let dashboard: Dashboard = serde_json::from_str(dashboard).unwrap();
        assert_eq!(dashboard.daily_hours.len(), 1);
        assert_eq!(dashboard.daily_hours[0].date, Date::parse("2026-11-17").unwrap());
        let report = r#"{"from":"2026-11-01","to":"2026-11-30","groupBy":"week","totalHours":4,"billableHours":3,"nonBillableHours":1,"totalAmount":300,"entryCount":2,"rows":[{"key":"2026-11-16","label":"Week of 2026-11-16","sublabel":"","color":"","hours":4,"billableHours":3,"amount":300,"entryCount":2}]}"#;
        let report: ReportSummary = serde_json::from_str(report).unwrap();
        assert_eq!(report.rows[0].label, "Week of 2026-11-16");
        assert_eq!(report.to, Date::parse("2026-11-30").unwrap());
    }

    #[test]
    fn meta_reads_the_demo_account() {
        let meta: MetaResponse = serde_json::from_str(r#"{"name":"WorkSite","version":"1","demoAccount":{"enabled":true,"username":"demo"}}"#).unwrap();
        assert!(meta.demo_account.enabled);
        assert_eq!(meta.demo_account.username, "demo");
        let meta: MetaResponse = serde_json::from_str(r#"{"name":"WorkSite","version":"1","demoAccount":{"enabled":false,"username":""}}"#).unwrap();
        assert_eq!(meta.demo_account.username, "");
    }

    #[test]
    fn requests_write_camel_case() {
        let body = ClientRequest {
            name: "Acme".into(),
            contact_name: "".into(),
            contact_email: "".into(),
            notes: "".into(),
            color: "teal".into(),
            is_archived: None,
        };
        let json = serde_json::to_string(&body).unwrap();
        assert_eq!(json, r#"{"name":"Acme","contactName":"","contactEmail":"","notes":"","color":"teal","isArchived":null}"#);

        let body = ProjectRequest {
            client_id: "c".into(),
            name: "P".into(),
            description: "".into(),
            hourly_rate: None,
            is_billable: true,
            is_archived: Some(false),
        };
        let json = serde_json::to_string(&body).unwrap();
        assert_eq!(json, r#"{"clientId":"c","name":"P","description":"","hourlyRate":null,"isBillable":true,"isArchived":false}"#);

        let body = TimeEntryRequest {
            project_id: "p".into(),
            work_date: Date::parse("2026-11-17").unwrap(),
            hours: 1.5,
            description: "d".into(),
            is_billable: false,
        };
        let json = serde_json::to_string(&body).unwrap();
        assert_eq!(json, r#"{"projectId":"p","workDate":"2026-11-17","hours":1.5,"description":"d","isBillable":false}"#);

        let json = serde_json::to_string(&ChangePasswordRequest { current_password: "a".into(), new_password: "b".into() }).unwrap();
        assert_eq!(json, r#"{"currentPassword":"a","newPassword":"b"}"#);
    }

    #[test]
    fn archive_toggles_keep_everything_but_the_flag() {
        let client = Client { id: "c1".into(), name: "Acme".into(), color: "teal".into(), notes: "n".into(), ..Default::default() };
        let body = ClientRequest::archive_toggle(&client, true);
        assert_eq!(body.name, "Acme");
        assert_eq!(body.notes, "n");
        assert_eq!(body.is_archived, Some(true));
        let project = Project { client_id: "c1".into(), name: "Site".into(), hourly_rate: Some(80.0), is_billable: true, is_archived: true, ..Default::default() };
        let body = ProjectRequest::archive_toggle(&project, false);
        assert_eq!(body.hourly_rate, Some(80.0));
        assert!(body.is_billable);
        assert_eq!(body.is_archived, Some(false));
    }

    #[test]
    fn time_log_paging_is_kept_within_the_limits() {
        let query = ListQuery {
            from: Date::parse("2026-11-01"),
            client_id: Some("c1".into()),
            billable: Some(false),
            ..Default::default()
        };
        let paged = query.clone().paged(2, 25);
        assert_eq!((paged.page, paged.page_size), (Some(2), Some(25)));
        assert_eq!(paged.client_id.as_deref(), Some("c1"));
        assert_eq!(paged.billable, Some(false));
        let clamped = query.paged(0, 500);
        assert_eq!((clamped.page, clamped.page_size), (Some(1), Some(100)));
    }

    #[test]
    fn report_filter_builds_summary_and_export_links() {
        let filter = ReportFilter {
            from: Date::parse("2026-11-01").unwrap(),
            to: Date::parse("2026-11-30").unwrap(),
            group_by: GroupBy::Project,
            client_id: None,
            project_id: Some("p 1".into()),
        };
        let summary = filter.summary_query();
        assert_eq!(summary.from, Date::parse("2026-11-01"));
        assert_eq!(summary.group_by.as_deref(), Some("project"));
        assert_eq!(summary.client_id, None);
        assert_eq!(summary.project_id.as_deref(), Some("p 1"));
        assert_eq!(filter.export_href(), "/api/reports/export.csv?from=2026-11-01&to=2026-11-30&projectId=p%201");
    }
}
