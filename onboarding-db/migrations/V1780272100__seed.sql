-- Onboarding Records
INSERT INTO onboarding.onboarding_records (
    onboarding_id, employee_id, status, started_at, completed_at
) VALUES
('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', '11111111-1111-1111-1111-111111111111', 'completed',   '2026-05-26 08:00:00+00', '2026-05-26 17:00:00+00'),
('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', '22222222-2222-2222-2222-222222222222', 'in_progress', '2026-05-26 08:00:00+00', NULL),
('cccccccc-cccc-cccc-cccc-cccccccccccc', '33333333-3333-3333-3333-333333333333', 'in_progress', '2026-05-26 08:00:00+00', NULL),
('dddddddd-dddd-dddd-dddd-dddddddddddd', '44444444-4444-4444-4444-444444444444', 'pending',     NULL,                    NULL),
('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', '55555555-5555-5555-5555-555555555555', 'failed',      '2026-05-26 08:00:00+00', NULL);

-- Account Tasks
INSERT INTO onboarding.tasks_accounts (task_id, onboarding_id, account_type, username, status, error_message, completed_at) VALUES
-- Alice (completed)
(gen_random_uuid(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'email',     'alice.tan@htx.gov.sg', 'completed', NULL, '2026-05-26 09:00:00+00'),
(gen_random_uuid(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'vpn',       'alice.tan@htx.gov.sg', 'completed', NULL, '2026-05-26 09:30:00+00'),
(gen_random_uuid(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'hr_portal', 'alice.tan@htx.gov.sg', 'completed', NULL, '2026-05-26 10:00:00+00'),
-- Bob (in_progress)
(gen_random_uuid(), 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'email',     'bob.lim@htx.gov.sg', 'completed', NULL, '2026-05-26 09:00:00+00'),
(gen_random_uuid(), 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'vpn',       'bob.lim@htx.gov.sg', 'pending',   NULL, NULL),
(gen_random_uuid(), 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'hr_portal', 'bob.lim@htx.gov.sg', 'pending',   NULL, NULL),
-- Carol (in_progress)
(gen_random_uuid(), 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'email',     'carol.ng@htx.gov.sg', 'completed', NULL, '2026-05-26 09:00:00+00'),
(gen_random_uuid(), 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'vpn',       'carol.ng@htx.gov.sg', 'completed', NULL, '2026-05-26 09:30:00+00'),
(gen_random_uuid(), 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'hr_portal', 'carol.ng@htx.gov.sg', 'pending',   NULL, NULL),
-- David (pending)
(gen_random_uuid(), 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'email',     NULL, 'pending', NULL, NULL),
(gen_random_uuid(), 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'vpn',       NULL, 'pending', NULL, NULL),
(gen_random_uuid(), 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'hr_portal', NULL, 'pending', NULL, NULL),
-- Eve (failed)
(gen_random_uuid(), 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'email',     NULL, 'failed',  'LDAP connection timeout',          NULL),
(gen_random_uuid(), 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'vpn',       NULL, 'failed',  'Upstream VPN service unavailable', NULL),
(gen_random_uuid(), 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'hr_portal', NULL, 'pending', NULL, NULL);

-- Equipment Tasks
INSERT INTO onboarding.tasks_equipment (task_id, onboarding_id, item_type, item_details, status, error_message, issued_at) VALUES
-- Alice (completed)
(gen_random_uuid(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'laptop',     '{"model": "Dell XPS 15", "serial": "DX15-001", "os": "Windows 11"}', 'issued',  NULL, '2026-05-26 11:00:00+00'),
(gen_random_uuid(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'staff_pass', '{"pass_number": "SP-2026-001", "access_level": "L2"}',               'issued',  NULL, '2026-05-26 11:30:00+00'),
(gen_random_uuid(), 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'welcome_kit','{"items": ["notebook", "pen", "lanyard", "htx_tshirt"]}',            'issued',  NULL, '2026-05-26 12:00:00+00'),
-- Bob (in_progress)
(gen_random_uuid(), 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'laptop',     '{"model": "MacBook Pro 14", "serial": "MBP14-001", "os": "macOS 15"}', 'issued', NULL, '2026-05-26 11:00:00+00'),
(gen_random_uuid(), 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'staff_pass', '{"pass_number": "SP-2026-002", "access_level": "L2"}',                 'pending',NULL, NULL),
(gen_random_uuid(), 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'welcome_kit','{"items": ["notebook", "pen", "lanyard", "htx_tshirt"]}',              'pending',NULL, NULL),
-- Carol (in_progress)
(gen_random_uuid(), 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'laptop',     '{"model": "Dell XPS 15", "serial": "DX15-002", "os": "Windows 11"}', 'issued',  NULL, '2026-05-26 11:00:00+00'),
(gen_random_uuid(), 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'staff_pass', '{"pass_number": "SP-2026-003", "access_level": "L3"}',               'issued',  NULL, '2026-05-26 11:30:00+00'),
(gen_random_uuid(), 'cccccccc-cccc-cccc-cccc-cccccccccccc', 'welcome_kit','{"items": ["notebook", "pen", "lanyard", "htx_tshirt"]}',            'pending', NULL, NULL),
-- David (pending)
(gen_random_uuid(), 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'laptop',     '{"model": "Dell XPS 15", "serial": "DX15-003", "os": "Windows 11"}', 'pending', NULL, NULL),
(gen_random_uuid(), 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'staff_pass', NULL, 'pending', NULL, NULL),
(gen_random_uuid(), 'dddddddd-dddd-dddd-dddd-dddddddddddd', 'welcome_kit',NULL, 'pending', NULL, NULL),
-- Eve (failed)
(gen_random_uuid(), 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'laptop',     NULL, 'failed',  'Inventory out of stock', NULL),
(gen_random_uuid(), 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'staff_pass', NULL, 'pending', NULL, NULL),
(gen_random_uuid(), 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', 'welcome_kit',NULL, 'pending', NULL, NULL);
