/* ===========================================================================
   DueMap — marketing demo workspace seed
   ---------------------------------------------------------------------------
   Creates a self-contained property manager ("Northgate Residential") with
   enough realistic volume to screenshot for the promo film: populated charts,
   a mixed-status lease list, notice history for the activity timeline, and a
   Today queue with real work in it.

   Everything is fictional. Tenant names are invented; the accounting
   connection is a display-only row with dummy tokens (never press Sync on
   this workspace -- the tokens can't decrypt).

   Removal: run unseed-demo-workspace.sql. Note that notice_deliveries is
   append-only via trigger, so that script disables the trigger for the
   duration of the delete and re-enables it.
=========================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @today   date = CAST(SYSUTCDATETIME() AS date);
DECLARE @now     datetime2 = SYSUTCDATETIME();
DECLARE @pm      int;
DECLARE @marker  nvarchar(20) = N'mkt-';   -- external_id prefix for cleanup

BEGIN TRAN;

/* ---------- 1. Property manager ---------------------------------------- */
INSERT tenancy.property_managers
    (name, created_at, onboarding_status, time_zone_id,
     step_connect_done_at, step_close_done_at, step_preflight_done_at, step_notice_prefs_done_at)
VALUES
    (N'Northgate Residential', DATEADD(day,-240,@now), 4, N'America/Chicago',
     DATEADD(day,-240,@now), DATEADD(day,-240,@now), DATEADD(day,-239,@now), DATEADD(day,-240,@now));
SET @pm = SCOPE_IDENTITY();

/* ---------- 2. Login (reuses the demo password hash) -------------------- */
INSERT [identity].[users]
    (property_manager_id, user_name, normalized_user_name, email, normalized_email,
     email_confirmed, password_hash, security_stamp, concurrency_stamp,
     phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count, created_at)
SELECT @pm, N'demo2@duemap.dev', N'DEMO2@DUEMAP.DEV', N'demo2@duemap.dev', N'DEMO2@DUEMAP.DEV',
       1, u.password_hash, NEWID(), NEWID(), 0, 0, 1, 0, @now
FROM [identity].[users] u WHERE u.normalized_email = N'DEMO@DUEMAP.DEV';

/* ---------- 3. Workspace settings --------------------------------------- */
INSERT tenancy.pm_notice_preferences
    (property_manager_id, pre_due_master_enabled, pre_due_default_days_before,
     due_date_master_enabled, post_due_master_enabled, post_due_default_mode,
     post_due_default_grace_days, updated_at)
VALUES (@pm, 1, 3, 1, 1, 'grace_period', 5, @now);

INSERT tenancy.pm_daily_close_settings
    (property_manager_id, enabled, send_hour_local, time_zone_id, updated_at)
VALUES (@pm, 1, 18, N'America/Chicago', @now);

/* Display-only connection. Dummy tokens: do NOT press Sync on this PM. */
INSERT integrations.pm_accounting_connections
    (property_manager_id, provider, realm_id, access_token_protected, refresh_token_protected,
     access_token_expires_at, refresh_token_expires_at, scopes, connected_at, last_sync_at,
     status, updated_at, health_status, company_name)
VALUES
    (@pm, 'xero', N'mkt-demo-realm', N'MKT_DEMO', N'MKT_DEMO',
     DATEADD(minute,25,@now), DATEADD(day,55,@now),
     N'accounting.contacts.read accounting.invoices.read',
     DATEADD(day,-240,@now), DATEADD(hour,-2,@now),
     'connected', @now, 0, N'Northgate Residential');

/* ---------- 4. Tally table ---------------------------------------------- */
IF OBJECT_ID('tempdb..#n') IS NOT NULL DROP TABLE #n;
SELECT TOP (500) n = ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1
INTO #n FROM sys.all_objects a CROSS JOIN sys.all_objects b;

/* ---------- 5. Customers (48) -------------------------------------------- */
DECLARE @first TABLE (i int, v nvarchar(30));
INSERT @first VALUES
 (0,N'Alice'),(1,N'Marcus'),(2,N'Priya'),(3,N'Devon'),(4,N'Sofia'),(5,N'Elena'),
 (6,N'Jamal'),(7,N'Nora'),(8,N'Tobias'),(9,N'Rina'),(10,N'Curtis'),(11,N'Yuki');
DECLARE @last TABLE (i int, v nvarchar(30));
INSERT @last VALUES
 (0,N'Whitfield'),(1,N'Okonkwo'),(2,N'Marchetti'),(3,N'Halvorsen');

INSERT tenancy.customers
    (property_manager_id, external_provider, external_id, display_name, email, phone,
     is_active, last_synced_at, created_at, updated_at, billing_state)
SELECT @pm, 'xero',
       @marker + N'cust-' + RIGHT('000' + CAST(n.n AS varchar(3)), 3),
       f.v + N' ' + l.v,
       LOWER(f.v) + N'.' + LOWER(l.v) + CAST(n.n AS nvarchar(3)) + N'@example.com',
       N'+1555' + RIGHT('0000000' + CAST(2000000 + n.n * 137 AS varchar(7)), 7),
       1, DATEADD(hour,-2,@now), DATEADD(day,-240,@now), @now,
       CASE n.n % 7 WHEN 0 THEN 'TX' WHEN 1 THEN 'CA' WHEN 2 THEN 'FL'
                    WHEN 3 THEN 'NY' WHEN 4 THEN 'GA' WHEN 5 THEN 'NC' ELSE 'AZ' END
FROM #n n
JOIN @first f ON f.i = n.n % 12
JOIN @last  l ON l.i = (n.n / 12) % 4
WHERE n.n < 48;

/* ---------- 6. Leases (44) ---------------------------------------------- */
/* Buckets by index:
     0-3    upcoming   (starts in the future)
     4-23   current    (paid / not yet due)
     24-29  in grace   (due 1-4 days ago)
     30-37  overdue    (due 6-40 days ago)
     38-40  late fee   (overdue + assessment posted)
     41-43  ended      (end_date in the past)                              */
IF OBJECT_ID('tempdb..#lease_src') IS NOT NULL DROP TABLE #lease_src;
SELECT
    idx        = n.n,
    customer_id= c.id,
    state_id   = CASE n.n % 7 WHEN 0 THEN 44 WHEN 1 THEN 5 WHEN 2 THEN 10
                              WHEN 3 THEN 33 WHEN 4 THEN 11 WHEN 5 THEN 34 ELSE 3 END,
    rent       = CAST(1150 + (n.n % 14) * 125 AS decimal(18,2)),
    unit       = N'Unit ' + CAST(100 + n.n AS nvarchar(4)) +
                 CASE n.n % 4 WHEN 0 THEN N'A' WHEN 1 THEN N'B' WHEN 2 THEN N'C' ELSE N'D' END,
    start_date = CASE WHEN n.n < 4 THEN DATEADD(day, 12 + n.n * 6, @today)
                      ELSE DATEADD(month, -(6 + (n.n % 14)), @today) END,
    end_date   = CASE WHEN n.n BETWEEN 41 AND 43 THEN DATEADD(day, -(20 + n.n), @today) END
INTO #lease_src
FROM #n n
JOIN tenancy.customers c
  ON c.property_manager_id = @pm
 AND c.external_id = @marker + N'cust-' + RIGHT('000' + CAST(n.n AS varchar(3)), 3)
WHERE n.n < 44;

INSERT tenancy.leases
    (property_manager_id, state_id, monthly_rent, start_date, end_date, created_at,
     customer_id, unit_label, late_fee_type, late_fee_percent, late_fee_grace_days,
     fees_staged, autopay_status)
SELECT @pm, s.state_id, s.rent, s.start_date, s.end_date, DATEADD(day,-240,@now),
       s.customer_id, s.unit,
       1, CAST(5.00 AS decimal(5,2)), 5,
       0,
       CASE WHEN s.idx % 5 = 0 THEN 1 ELSE 0 END          -- some on autopay
FROM #lease_src s
ORDER BY s.idx;

/* ---------- 7. Invoices — 6 months per active lease ---------------------- */
/* Older months paid; the current month's state depends on the lease bucket. */
IF OBJECT_ID('tempdb..#l') IS NOT NULL DROP TABLE #l;
SELECT l.id, l.customer_id, l.monthly_rent, s.idx
INTO #l
FROM tenancy.leases l
JOIN #lease_src s ON s.customer_id = l.customer_id
WHERE l.property_manager_id = @pm;

INSERT tenancy.rent_invoices
    (property_manager_id, customer_id, lease_id, external_provider, external_id,
     external_doc_number, issue_date, due_date, total_amount, balance, currency,
     status, last_synced_at, created_at, updated_at)
SELECT
    @pm, l.customer_id, l.id, 'xero',
    @marker + N'inv-' + CAST(l.id AS nvarchar(6)) + N'-' + CAST(m.n AS nvarchar(2)),
    N'INV-' + RIGHT('0000' + CAST(l.id * 10 + m.n AS varchar(5)), 5),
    DATEADD(day,-6, due.d), due.d,
    l.monthly_rent,
    /* balance */
    CASE WHEN m.n > 0 THEN 0                                   -- past months paid
         WHEN l.idx < 4  THEN 0                                 -- upcoming: none due
         WHEN l.idx BETWEEN 4 AND 23 THEN 0                     -- current: paid
         ELSE l.monthly_rent END,                               -- grace/overdue/fee
    'USD',
    CASE WHEN m.n > 0 THEN 'paid'
         WHEN l.idx BETWEEN 4 AND 23 THEN 'paid'
         WHEN l.idx < 4 THEN 'open'
         ELSE 'open' END,
    DATEADD(hour,-2,@now), DATEADD(day,-200,@now), @now
FROM #l l
CROSS JOIN (SELECT n FROM #n WHERE n < 6) m
CROSS APPLY (SELECT d = CASE
        WHEN m.n = 0 THEN
            CASE WHEN l.idx BETWEEN 24 AND 29 THEN DATEADD(day, -(1 + l.idx % 4), @today)   -- in grace
                 WHEN l.idx BETWEEN 30 AND 37 THEN DATEADD(day, -(8 + l.idx % 30), @today) -- overdue
                 WHEN l.idx BETWEEN 38 AND 40 THEN DATEADD(day, -(26 + l.idx % 12), @today)-- late fee
                 WHEN l.idx < 4 THEN DATEADD(day, 20 + l.idx, @today)                       -- upcoming
                 ELSE DATEADD(day, -(l.idx % 20), @today) END                                -- current
        ELSE DATEADD(month, -m.n, DATEADD(day, -(l.idx % 20), @today)) END) due
WHERE l.idx NOT BETWEEN 41 AND 43;    -- ended leases: history only, added below

/* Ended leases get history but nothing current */
INSERT tenancy.rent_invoices
    (property_manager_id, customer_id, lease_id, external_provider, external_id,
     external_doc_number, issue_date, due_date, total_amount, balance, currency,
     status, last_synced_at, created_at, updated_at)
SELECT @pm, l.customer_id, l.id, 'xero',
    @marker + N'inv-' + CAST(l.id AS nvarchar(6)) + N'-e' + CAST(m.n AS nvarchar(2)),
    N'INV-' + RIGHT('0000' + CAST(l.id * 10 + 90 + m.n AS varchar(5)), 5),
    DATEADD(day,-6, DATEADD(month,-(m.n+2), @today)), DATEADD(month,-(m.n+2), @today),
    l.monthly_rent, 0, 'USD', 'paid',
    DATEADD(hour,-2,@now), DATEADD(day,-200,@now), @now
FROM #l l CROSS JOIN (SELECT n FROM #n WHERE n < 4) m
WHERE l.idx BETWEEN 41 AND 43;

/* ---------- 8. Notice history (drives timelines + Daily Close badges) ---- */
DECLARE @tv_predue int = (SELECT MAX(v.id) FROM notices.notice_template_versions v
    JOIN notices.notice_templates t ON t.id=v.template_id
    JOIN notices.notice_types nt ON nt.id=t.notice_type_id
    WHERE nt.code='pre_due_reminder' AND t.state_id IS NULL);
DECLARE @tv_due int = (SELECT MAX(v.id) FROM notices.notice_template_versions v
    JOIN notices.notice_templates t ON t.id=v.template_id
    JOIN notices.notice_types nt ON nt.id=t.notice_type_id
    WHERE nt.code='due_date_reminder' AND t.state_id IS NULL);
DECLARE @tv_grace int = (SELECT MAX(v.id) FROM notices.notice_template_versions v
    JOIN notices.notice_templates t ON t.id=v.template_id
    JOIN notices.notice_types nt ON nt.id=t.notice_type_id
    WHERE nt.code='grace_period_reminder' AND t.state_id IS NULL);
DECLARE @tv_fee int = (SELECT MAX(v.id) FROM notices.notice_template_versions v
    JOIN notices.notice_templates t ON t.id=v.template_id
    JOIN notices.notice_types nt ON nt.id=t.notice_type_id
    WHERE nt.code='late_fee_notice' AND t.state_id IS NULL);

/* Pre-due reminder — everyone with a current-month invoice */
INSERT notices.notice_deliveries
    (property_manager_id, lease_id, template_version_id, rendered_subject,
     rendered_body_html, rendered_body_text, channel, sent_at, provider_msg_id,
     status, delivered_at, created_at)
SELECT @pm, l.id, @tv_predue,
       N'Rent due in 3 days',
       N'<p>A friendly reminder that rent is due soon.</p>',
       N'A friendly reminder that rent is due soon.',
       'email', DATEADD(day,-(9 + l.idx % 6), @now),
       N'mkt-' + CAST(NEWID() AS nvarchar(36)),
       'delivered', DATEADD(day,-(9 + l.idx % 6), DATEADD(minute,2,@now)), @now
FROM #l l WHERE l.idx BETWEEN 4 AND 40;

/* Due-date reminder — grace + overdue + fee buckets */
INSERT notices.notice_deliveries
    (property_manager_id, lease_id, template_version_id, rendered_subject,
     rendered_body_html, rendered_body_text, channel, sent_at, provider_msg_id,
     status, delivered_at, created_at)
SELECT @pm, l.id, @tv_due,
       N'Rent is due today',
       N'<p>Rent is due today.</p>', N'Rent is due today.',
       'email', DATEADD(day,-(4 + l.idx % 5), @now),
       N'mkt-' + CAST(NEWID() AS nvarchar(36)),
       'delivered', DATEADD(day,-(4 + l.idx % 5), DATEADD(minute,2,@now)), @now
FROM #l l WHERE l.idx BETWEEN 24 AND 40;

/* Grace-period reminder — overdue + fee buckets */
INSERT notices.notice_deliveries
    (property_manager_id, lease_id, template_version_id, rendered_subject,
     rendered_body_html, rendered_body_text, channel, sent_at, provider_msg_id,
     status, delivered_at, created_at)
SELECT @pm, l.id, @tv_grace,
       N'Rent is past due',
       N'<p>Your rent payment is past due.</p>', N'Your rent payment is past due.',
       'email', DATEADD(day,-(3 + l.idx % 4), @now),
       N'mkt-' + CAST(NEWID() AS nvarchar(36)),
       'delivered', DATEADD(day,-(3 + l.idx % 4), DATEADD(minute,2,@now)), @now
FROM #l l WHERE l.idx BETWEEN 30 AND 40;

/* Late-fee warning — fee bucket only, plus one deliberate bounce for Today */
INSERT notices.notice_deliveries
    (property_manager_id, lease_id, template_version_id, rendered_subject,
     rendered_body_html, rendered_body_text, channel, sent_at, provider_msg_id,
     status, delivered_at, bounced_at, created_at)
SELECT @pm, l.id, @tv_fee,
       N'A late fee may apply to your account',
       N'<p>A late fee may apply under your state''s rules.</p>',
       N'A late fee may apply under your state''s rules.',
       'email', DATEADD(day,-(1 + l.idx % 3), @now),
       N'mkt-' + CAST(NEWID() AS nvarchar(36)),
       CASE WHEN l.idx = 39 THEN 'bounced' ELSE 'delivered' END,
       CASE WHEN l.idx = 39 THEN NULL ELSE DATEADD(day,-(1 + l.idx % 3), DATEADD(minute,2,@now)) END,
       CASE WHEN l.idx = 39 THEN DATEADD(day,-(1 + l.idx % 3), DATEADD(minute,3,@now)) END,
       @now
FROM #l l WHERE l.idx BETWEEN 38 AND 40;

/* ---------- 9. Late-fee assessments (fee bucket) ------------------------- */
INSERT billing.late_fee_assessments
    (lease_id, due_date, assessment_date, fee_amount, monthly_rent_snapshot,
     state_rule_version_id, status, created_at)
SELECT l.id,
       DATEADD(day,-(26 + l.idx % 12), @today),
       DATEADD(day,-(21 + l.idx % 12), @today),
       CAST(l.monthly_rent * 0.05 AS decimal(18,2)),
       l.monthly_rent,
       COALESCE((SELECT TOP 1 v.id FROM rules.state_rule_versions v
                 JOIN tenancy.leases lx ON lx.id = l.id AND lx.state_id = v.state_id), 1),
       'assessed', @now
FROM #l l WHERE l.idx BETWEEN 38 AND 40;

/* ---------- 10. Promise to pay (pauses one lease) ------------------------ */
INSERT tenancy.payment_promises
    (property_manager_id, lease_id, amount, promised_date, note, status, created_at)
SELECT TOP 1 @pm, l.id, l.monthly_rent, DATEADD(day, 3, @today),
       N'Called — paycheck lands Friday, will pay in full.', 1, DATEADD(day,-2,@now)
FROM #l l WHERE l.idx = 31;

/* ---------- 11. Processing runs (last 14 days) --------------------------- */
INSERT billing.pm_processing_runs
    (property_manager_id, business_date, started_at, completed_at, status,
     leases_planned, actions_executed)
SELECT @pm, DATEADD(day,-n.n,@today),
       DATEADD(day,-n.n, DATEADD(hour,-6,@now)),
       DATEADD(day,-n.n, DATEADD(hour,-6, DATEADD(second,42,@now))),
       'completed', 41, 3 + (n.n % 5)
FROM #n n WHERE n.n < 14;

COMMIT;

/* ---------- Summary ------------------------------------------------------ */
SELECT 'property_manager_id' = @pm;
SELECT 'customers'  = COUNT(*) FROM tenancy.customers  WHERE property_manager_id = @pm;
SELECT 'leases'     = COUNT(*) FROM tenancy.leases     WHERE property_manager_id = @pm;
SELECT 'invoices'   = COUNT(*) FROM tenancy.rent_invoices WHERE property_manager_id = @pm;
SELECT 'notices'    = COUNT(*) FROM notices.notice_deliveries WHERE property_manager_id = @pm;
SELECT 'runs'       = COUNT(*) FROM billing.pm_processing_runs WHERE property_manager_id = @pm;
