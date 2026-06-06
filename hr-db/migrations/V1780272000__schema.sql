CREATE TABLE hr.employees (
    employee_id     UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    employee_number VARCHAR(50)  UNIQUE NOT NULL,
    first_name      VARCHAR(100) NOT NULL,
    last_name       VARCHAR(100) NOT NULL,
    email           VARCHAR(255) UNIQUE NOT NULL,
    department      VARCHAR(100),
    position        VARCHAR(100),
    hire_date       DATE         NOT NULL,
    status          VARCHAR(20)  NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'inactive')),
    created_at      TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at      TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_employees_email  ON hr.employees(email);
CREATE INDEX idx_employees_number ON hr.employees(employee_number);
CREATE INDEX idx_employees_status ON hr.employees(status);

CREATE OR REPLACE FUNCTION hr.update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

CREATE TRIGGER update_employees_updated_at
    BEFORE UPDATE ON hr.employees
    FOR EACH ROW
    EXECUTE FUNCTION hr.update_updated_at_column();

GRANT USAGE ON SCHEMA hr TO hr_svc;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA hr TO hr_svc;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA hr TO hr_svc;
