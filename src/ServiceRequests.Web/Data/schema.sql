-- Schema for requests submitted through the form.
-- Safe to run again: it only creates what does not exist yet and never drops
-- or deletes data. DatabaseInitializer runs it on every application start.

CREATE TABLE IF NOT EXISTS service_requests (
    id          uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    name        text        NOT NULL CHECK (char_length(name) BETWEEN 2 AND 100),
    email       text        NOT NULL CHECK (char_length(email) <= 254),
    service     text        NOT NULL CHECK (service IN ('workflow-automation', 'api-integration', 'ai-triage')),
    description text        NOT NULL CHECK (char_length(description) BETWEEN 10 AND 2000),
    created_at  timestamptz NOT NULL DEFAULT now()
);
