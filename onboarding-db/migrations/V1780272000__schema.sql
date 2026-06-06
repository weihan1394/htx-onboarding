CREATE TABLE onboarding.onboarding_records (
    onboarding_id UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    employee_id   UUID         NOT NULL,
    status        VARCHAR(20)  NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'in_progress', 'completed', 'failed')),
    retry_count   INT          NOT NULL DEFAULT 0,
    started_at    TIMESTAMP WITH TIME ZONE,
    completed_at  TIMESTAMP WITH TIME ZONE,
    created_at    TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at    TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE onboarding.tasks_accounts (
    task_id       UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    onboarding_id UUID        NOT NULL REFERENCES onboarding.onboarding_records(onboarding_id) ON DELETE CASCADE,
    account_type  VARCHAR(50) NOT NULL,
    username      VARCHAR(100),
    status        VARCHAR(20) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'completed', 'failed')),
    error_message TEXT,
    created_at    TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    completed_at  TIMESTAMP WITH TIME ZONE,
    CONSTRAINT uq_accounts_onboarding_type UNIQUE (onboarding_id, account_type)
);

CREATE TABLE onboarding.tasks_equipment (
    task_id       UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    onboarding_id UUID        NOT NULL REFERENCES onboarding.onboarding_records(onboarding_id) ON DELETE CASCADE,
    item_type     VARCHAR(50) NOT NULL CHECK (item_type IN ('laptop', 'staff_pass', 'welcome_kit')),
    item_details  JSONB,
    status        VARCHAR(20) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'issued', 'failed')),
    error_message TEXT,
    issued_at     TIMESTAMP WITH TIME ZONE,
    created_at    TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_equipment_onboarding_type UNIQUE (onboarding_id, item_type)
);

-- tracks every terminal outcome (completed or failed) per attempt
CREATE TABLE onboarding.onboarding_transactions (
    history_id    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    onboarding_id UUID        NOT NULL REFERENCES onboarding.onboarding_records(onboarding_id),
    attempt       INT         NOT NULL,
    status        VARCHAR(20) NOT NULL DEFAULT 'failed',
    error_message TEXT,
    attempted_at  TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_onboarding_employee  ON onboarding.onboarding_records(employee_id);
CREATE INDEX idx_onboarding_status    ON onboarding.onboarding_records(status);
CREATE INDEX idx_accounts_onboarding  ON onboarding.tasks_accounts(onboarding_id);
CREATE INDEX idx_equipment_onboarding ON onboarding.tasks_equipment(onboarding_id);

CREATE OR REPLACE FUNCTION onboarding.update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

CREATE TRIGGER update_onboarding_records_updated_at
    BEFORE UPDATE ON onboarding.onboarding_records
    FOR EACH ROW
    EXECUTE FUNCTION onboarding.update_updated_at_column();

GRANT USAGE ON SCHEMA onboarding TO onboarding_svc;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA onboarding TO onboarding_svc;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA onboarding TO onboarding_svc;
