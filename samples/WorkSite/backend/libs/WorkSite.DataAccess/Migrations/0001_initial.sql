-- The first WorkSite schema: users, their sign-in sessions, and the clients, projects and time
-- entries each user records. Every row below users carries the owning user_id, and the composite
-- foreign keys make it impossible for a project or time entry to point at another user's rows.

CREATE TABLE users (
    id              uuid         PRIMARY KEY,
    first_name      varchar(100) NOT NULL,
    last_name       varchar(100) NOT NULL,
    email           varchar(254) NOT NULL,
    username        varchar(32)  NOT NULL,
    password_hash   text         NOT NULL,
    email_verified  boolean      NOT NULL DEFAULT false,
    is_demo         boolean      NOT NULL DEFAULT false,
    created_at      timestamptz  NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_users_email ON users (lower(email));
CREATE UNIQUE INDEX ux_users_username ON users (lower(username));

-- One row per sign-in. Only SHA-256 hashes of refresh tokens are stored, never the tokens.
CREATE TABLE sessions (
    id                           uuid        PRIMARY KEY,
    user_id                      uuid        NOT NULL REFERENCES users (id),
    refresh_token_hash           bytea       NOT NULL,
    previous_refresh_token_hash  bytea,
    created_at                   timestamptz NOT NULL DEFAULT now(),
    last_refreshed_at            timestamptz,
    expires_at                   timestamptz NOT NULL,
    revoked_at                   timestamptz
);

CREATE UNIQUE INDEX ux_sessions_refresh_token_hash ON sessions (refresh_token_hash);
CREATE INDEX ix_sessions_previous_refresh_token_hash ON sessions (previous_refresh_token_hash)
    WHERE previous_refresh_token_hash IS NOT NULL;
CREATE INDEX ix_sessions_user_id ON sessions (user_id);

CREATE TABLE clients (
    id             uuid         PRIMARY KEY,
    user_id        uuid         NOT NULL REFERENCES users (id),
    name           varchar(120) NOT NULL,
    contact_name   varchar(120) NOT NULL DEFAULT '',
    contact_email  varchar(254) NOT NULL DEFAULT '',
    notes          text         NOT NULL DEFAULT '',
    color          varchar(16)  NOT NULL DEFAULT 'slate',
    is_archived    boolean      NOT NULL DEFAULT false,
    created_at     timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_clients_color CHECK (color IN
        ('slate', 'red', 'orange', 'amber', 'green', 'teal', 'sky', 'indigo', 'violet', 'pink')),
    CONSTRAINT ux_clients_id_user UNIQUE (id, user_id)
);

CREATE UNIQUE INDEX ux_clients_user_name ON clients (user_id, lower(name));

CREATE TABLE projects (
    id           uuid          PRIMARY KEY,
    user_id      uuid          NOT NULL,
    client_id    uuid          NOT NULL,
    name         varchar(120)  NOT NULL,
    description  text          NOT NULL DEFAULT '',
    hourly_rate  numeric(10,2),
    is_billable  boolean       NOT NULL DEFAULT true,
    is_archived  boolean       NOT NULL DEFAULT false,
    created_at   timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT ck_projects_hourly_rate CHECK (hourly_rate IS NULL OR (hourly_rate >= 0 AND hourly_rate <= 10000)),
    CONSTRAINT fk_projects_client FOREIGN KEY (client_id, user_id) REFERENCES clients (id, user_id),
    CONSTRAINT ux_projects_id_user UNIQUE (id, user_id)
);

CREATE UNIQUE INDEX ux_projects_client_name ON projects (client_id, lower(name));
CREATE INDEX ix_projects_user_id ON projects (user_id);

CREATE TABLE time_entries (
    id           uuid          PRIMARY KEY,
    user_id      uuid          NOT NULL,
    project_id   uuid          NOT NULL,
    work_date    date          NOT NULL,
    hours        numeric(5,2)  NOT NULL,
    description  varchar(500)  NOT NULL DEFAULT '',
    is_billable  boolean       NOT NULL,
    created_at   timestamptz   NOT NULL DEFAULT now(),
    updated_at   timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT ck_time_entries_hours CHECK (hours > 0 AND hours <= 24),
    CONSTRAINT fk_time_entries_project FOREIGN KEY (project_id, user_id) REFERENCES projects (id, user_id)
);

CREATE INDEX ix_time_entries_user_work_date ON time_entries (user_id, work_date DESC, created_at DESC);
CREATE INDEX ix_time_entries_project_id ON time_entries (project_id);
