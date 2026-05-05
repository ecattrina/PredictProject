-- MySQL 8+, InnoDB, utf8mb4 (опционально: текущий проект по умолчанию использует SQLite — см. appsettings.json)
-- Создайте БД и выполните скрипт, если переключите провайдер обратно на MySQL.

CREATE DATABASE IF NOT EXISTS forecast_payments
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

USE forecast_payments;

CREATE TABLE roles (
  id BIGINT NOT NULL AUTO_INCREMENT,
  name VARCHAR(64) NOT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  deleted_at DATETIME(3) NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_roles_name (name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE users (
  id BIGINT NOT NULL AUTO_INCREMENT,
  email VARCHAR(256) NOT NULL,
  password_hash VARCHAR(500) NOT NULL,
  full_name VARCHAR(256) NOT NULL,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  deleted_at DATETIME(3) NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_users_email (email),
  KEY ix_users_deleted (deleted_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE user_roles (
  user_id BIGINT NOT NULL,
  role_id BIGINT NOT NULL,
  assigned_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (user_id, role_id),
  CONSTRAINT fk_user_roles_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_user_roles_role FOREIGN KEY (role_id) REFERENCES roles (id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE suppliers (
  id BIGINT NOT NULL AUTO_INCREMENT,
  supplier_code VARCHAR(64) NOT NULL,
  name VARCHAR(512) NOT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  deleted_at DATETIME(3) NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_suppliers_code_deleted (supplier_code, deleted_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE contracts (
  id BIGINT NOT NULL AUTO_INCREMENT,
  supplier_id BIGINT NOT NULL,
  internal_contract_number VARCHAR(128) NOT NULL,
  external_contract_number VARCHAR(128) NULL,
  contract_date DATE NULL,
  contract_amount DECIMAL(18,2) NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  deleted_at DATETIME(3) NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_contract_natural (supplier_id, internal_contract_number, deleted_at),
  KEY ix_contracts_supplier (supplier_id),
  CONSTRAINT fk_contracts_supplier FOREIGN KEY (supplier_id) REFERENCES suppliers (id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE import_batches (
  id BIGINT NOT NULL AUTO_INCREMENT,
  uploaded_by_user_id BIGINT NOT NULL,
  original_file_name VARCHAR(512) NOT NULL,
  file_hash CHAR(64) NOT NULL,
  file_size_bytes BIGINT NOT NULL,
  storage_path VARCHAR(1024) NOT NULL,
  mime_type VARCHAR(128) NULL,
  status VARCHAR(32) NOT NULL,
  committed_at DATETIME(3) NULL,
  rolled_back_at DATETIME(3) NULL,
  notes VARCHAR(512) NULL,
  detected_blocks_json JSON NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY ix_import_batches_status (status),
  KEY ix_import_batches_hash (file_hash),
  CONSTRAINT fk_import_batches_user FOREIGN KEY (uploaded_by_user_id) REFERENCES users (id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE contract_conditions (
  id BIGINT NOT NULL AUTO_INCREMENT,
  contract_id BIGINT NOT NULL,
  valid_from DATE NOT NULL,
  valid_to DATE NULL,
  payment_delay_days INT NOT NULL,
  description VARCHAR(512) NULL,
  source_import_batch_id BIGINT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  deleted_at DATETIME(3) NULL,
  PRIMARY KEY (id),
  KEY ix_cc_contract (contract_id),
  KEY ix_cc_dates (contract_id, valid_from, valid_to),
  CONSTRAINT fk_cc_contract FOREIGN KEY (contract_id) REFERENCES contracts (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_cc_import FOREIGN KEY (source_import_batch_id) REFERENCES import_batches (id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE business_calendar_days (
  calendar_code VARCHAR(32) NOT NULL DEFAULT 'default',
  calendar_date DATE NOT NULL,
  is_working_day TINYINT(1) NOT NULL,
  note VARCHAR(255) NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  deleted_at DATETIME(3) NULL,
  PRIMARY KEY (calendar_code, calendar_date),
  KEY ix_cal_date (calendar_date)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE import_rows (
  id BIGINT NOT NULL AUTO_INCREMENT,
  import_batch_id BIGINT NOT NULL,
  sheet_name VARCHAR(128) NOT NULL,
  row_number INT NOT NULL,
  raw_json JSON NOT NULL,
  detected_entity_type VARCHAR(32) NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY ix_import_rows_batch (import_batch_id),
  CONSTRAINT fk_import_rows_batch FOREIGN KEY (import_batch_id) REFERENCES import_batches (id) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE import_errors (
  id BIGINT NOT NULL AUTO_INCREMENT,
  import_batch_id BIGINT NOT NULL,
  import_row_id BIGINT NULL,
  sheet_name VARCHAR(128) NULL,
  row_number INT NULL,
  column_name VARCHAR(128) NULL,
  error_code VARCHAR(64) NOT NULL,
  severity VARCHAR(16) NOT NULL,
  message VARCHAR(1024) NOT NULL,
  details_json JSON NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY ix_import_errors_batch (import_batch_id, severity),
  CONSTRAINT fk_import_errors_batch FOREIGN KEY (import_batch_id) REFERENCES import_batches (id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_import_errors_row FOREIGN KEY (import_row_id) REFERENCES import_rows (id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE staging_suppliers (
  id BIGINT NOT NULL AUTO_INCREMENT,
  import_batch_id BIGINT NOT NULL,
  import_row_id BIGINT NULL,
  supplier_code VARCHAR(64) NOT NULL,
  name VARCHAR(512) NOT NULL,
  row_number INT NOT NULL,
  sheet_name VARCHAR(128) NOT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY ix_stg_sup_batch (import_batch_id),
  CONSTRAINT fk_stg_sup_batch FOREIGN KEY (import_batch_id) REFERENCES import_batches (id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_stg_sup_row FOREIGN KEY (import_row_id) REFERENCES import_rows (id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE staging_contracts (
  id BIGINT NOT NULL AUTO_INCREMENT,
  import_batch_id BIGINT NOT NULL,
  import_row_id BIGINT NULL,
  supplier_code VARCHAR(64) NOT NULL,
  internal_contract_number VARCHAR(128) NOT NULL,
  external_contract_number VARCHAR(128) NULL,
  contract_date DATE NULL,
  contract_amount DECIMAL(18,2) NULL,
  row_number INT NOT NULL,
  sheet_name VARCHAR(128) NOT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY ix_stg_ctr_batch (import_batch_id),
  CONSTRAINT fk_stg_ctr_batch FOREIGN KEY (import_batch_id) REFERENCES import_batches (id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_stg_ctr_row FOREIGN KEY (import_row_id) REFERENCES import_rows (id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE staging_contract_conditions (
  id BIGINT NOT NULL AUTO_INCREMENT,
  import_batch_id BIGINT NOT NULL,
  import_row_id BIGINT NULL,
  contract_key VARCHAR(256) NOT NULL,
  internal_contract_number VARCHAR(128) NULL,
  supplier_code VARCHAR(64) NULL,
  payment_delay_days INT NOT NULL,
  valid_from DATE NULL,
  valid_to DATE NULL,
  row_number INT NOT NULL,
  sheet_name VARCHAR(128) NOT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY ix_stg_cc_batch (import_batch_id),
  CONSTRAINT fk_stg_cc_batch FOREIGN KEY (import_batch_id) REFERENCES import_batches (id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_stg_cc_row FOREIGN KEY (import_row_id) REFERENCES import_rows (id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE staging_incoming_debts (
  id BIGINT NOT NULL AUTO_INCREMENT,
  import_batch_id BIGINT NOT NULL,
  import_row_id BIGINT NULL,
  supplier_code VARCHAR(64) NOT NULL,
  internal_contract_number VARCHAR(128) NOT NULL,
  debt_date DATE NOT NULL,
  debt_amount DECIMAL(18,2) NOT NULL,
  row_number INT NOT NULL,
  sheet_name VARCHAR(128) NOT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY ix_stg_debt_batch (import_batch_id),
  CONSTRAINT fk_stg_debt_batch FOREIGN KEY (import_batch_id) REFERENCES import_batches (id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_stg_debt_row FOREIGN KEY (import_row_id) REFERENCES import_rows (id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE staging_actual_shipments (
  id BIGINT NOT NULL AUTO_INCREMENT,
  import_batch_id BIGINT NOT NULL,
  import_row_id BIGINT NULL,
  supplier_code VARCHAR(64) NOT NULL,
  internal_contract_number VARCHAR(128) NOT NULL,
  order_number VARCHAR(128) NOT NULL DEFAULT '',
  shipment_doc_number VARCHAR(128) NOT NULL,
  shipment_doc_date DATE NOT NULL,
  shipment_amount DECIMAL(18,2) NOT NULL,
  row_number INT NOT NULL,
  sheet_name VARCHAR(128) NOT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY ix_stg_ship_batch (import_batch_id),
  CONSTRAINT fk_stg_ship_batch FOREIGN KEY (import_batch_id) REFERENCES import_batches (id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_stg_ship_row FOREIGN KEY (import_row_id) REFERENCES import_rows (id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE staging_supplier_orders (
  id BIGINT NOT NULL AUTO_INCREMENT,
  import_batch_id BIGINT NOT NULL,
  import_row_id BIGINT NULL,
  supplier_code VARCHAR(64) NOT NULL,
  internal_contract_number VARCHAR(128) NOT NULL,
  order_number VARCHAR(128) NOT NULL,
  order_date DATE NULL,
  order_amount DECIMAL(18,2) NULL,
  row_number INT NOT NULL,
  sheet_name VARCHAR(128) NOT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY ix_stg_ord_batch (import_batch_id),
  CONSTRAINT fk_stg_ord_batch FOREIGN KEY (import_batch_id) REFERENCES import_batches (id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_stg_ord_row FOREIGN KEY (import_row_id) REFERENCES import_rows (id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE incoming_debt_snapshots (
  id BIGINT NOT NULL AUTO_INCREMENT,
  supplier_id BIGINT NOT NULL,
  contract_id BIGINT NOT NULL,
  debt_date DATE NOT NULL,
  debt_amount DECIMAL(18,2) NOT NULL,
  currency_code CHAR(3) NOT NULL DEFAULT 'RUB',
  source_import_batch_id BIGINT NOT NULL,
  source_row_hash CHAR(64) NOT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  deleted_at DATETIME(3) NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_debt_natural (supplier_id, contract_id, debt_date, deleted_at),
  KEY ix_debt_batch (source_import_batch_id),
  CONSTRAINT fk_debt_supplier FOREIGN KEY (supplier_id) REFERENCES suppliers (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_debt_contract FOREIGN KEY (contract_id) REFERENCES contracts (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_debt_batch FOREIGN KEY (source_import_batch_id) REFERENCES import_batches (id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE actual_shipments (
  id BIGINT NOT NULL AUTO_INCREMENT,
  supplier_id BIGINT NOT NULL,
  contract_id BIGINT NOT NULL,
  order_number VARCHAR(128) NOT NULL DEFAULT '',
  shipment_doc_number VARCHAR(128) NOT NULL,
  shipment_doc_date DATE NOT NULL,
  shipment_amount DECIMAL(18,2) NOT NULL,
  currency_code CHAR(3) NOT NULL DEFAULT 'RUB',
  source_import_batch_id BIGINT NOT NULL,
  source_row_hash CHAR(64) NOT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  deleted_at DATETIME(3) NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_ship_natural (supplier_id, contract_id, shipment_doc_number, shipment_doc_date, order_number, deleted_at),
  KEY ix_ship_contract_date (contract_id, shipment_doc_date),
  CONSTRAINT fk_ship_supplier FOREIGN KEY (supplier_id) REFERENCES suppliers (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_ship_contract FOREIGN KEY (contract_id) REFERENCES contracts (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_ship_batch FOREIGN KEY (source_import_batch_id) REFERENCES import_batches (id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE supplier_orders (
  id BIGINT NOT NULL AUTO_INCREMENT,
  supplier_id BIGINT NOT NULL,
  contract_id BIGINT NOT NULL,
  order_number VARCHAR(128) NOT NULL,
  order_date DATE NULL,
  order_amount DECIMAL(18,2) NULL,
  currency_code CHAR(3) NOT NULL DEFAULT 'RUB',
  source_import_batch_id BIGINT NOT NULL,
  source_row_hash CHAR(64) NOT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  deleted_at DATETIME(3) NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_order_natural (supplier_id, contract_id, order_number, deleted_at),
  CONSTRAINT fk_ord_supplier FOREIGN KEY (supplier_id) REFERENCES suppliers (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_ord_contract FOREIGN KEY (contract_id) REFERENCES contracts (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_ord_batch FOREIGN KEY (source_import_batch_id) REFERENCES import_batches (id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE actual_payments (
  id BIGINT NOT NULL AUTO_INCREMENT,
  supplier_id BIGINT NOT NULL,
  contract_id BIGINT NOT NULL,
  payment_date DATE NOT NULL,
  amount DECIMAL(18,2) NOT NULL,
  currency_code CHAR(3) NOT NULL DEFAULT 'RUB',
  document_number VARCHAR(128) NULL,
  source_import_batch_id BIGINT NOT NULL,
  source_row_hash CHAR(64) NOT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  deleted_at DATETIME(3) NULL,
  PRIMARY KEY (id),
  KEY ix_pay_contract_date (contract_id, payment_date),
  CONSTRAINT fk_pay_supplier FOREIGN KEY (supplier_id) REFERENCES suppliers (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_pay_contract FOREIGN KEY (contract_id) REFERENCES contracts (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_pay_batch FOREIGN KEY (source_import_batch_id) REFERENCES import_batches (id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE payment_calculation_runs (
  id BIGINT NOT NULL AUTO_INCREMENT,
  run_code CHAR(36) NOT NULL,
  calculation_date DATE NOT NULL,
  calendar_code VARCHAR(32) NOT NULL DEFAULT 'default',
  filter_supplier_id BIGINT NULL,
  filter_contract_id BIGINT NULL,
  status VARCHAR(16) NOT NULL,
  archived TINYINT(1) NOT NULL DEFAULT 0,
  started_by_user_id BIGINT NOT NULL,
  started_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  finished_at DATETIME(3) NULL,
  parameters_json JSON NULL,
  error_message VARCHAR(2000) NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  deleted_at DATETIME(3) NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_run_code (run_code),
  KEY ix_run_calc_date (calculation_date),
  CONSTRAINT fk_run_user FOREIGN KEY (started_by_user_id) REFERENCES users (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_run_supplier FOREIGN KEY (filter_supplier_id) REFERENCES suppliers (id) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT fk_run_contract FOREIGN KEY (filter_contract_id) REFERENCES contracts (id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE payment_schedule_items (
  id BIGINT NOT NULL AUTO_INCREMENT,
  calculation_run_id BIGINT NOT NULL,
  supplier_id BIGINT NOT NULL,
  contract_id BIGINT NOT NULL,
  incoming_debt_snapshot_id BIGINT NOT NULL,
  actual_shipment_id BIGINT NOT NULL,
  order_number VARCHAR(128) NOT NULL DEFAULT '',
  shipment_doc_number VARCHAR(128) NOT NULL,
  shipment_doc_date DATE NOT NULL,
  pay_amount DECIMAL(18,2) NOT NULL,
  due_date_by_condition DATE NOT NULL,
  forecast_payment_date DATE NOT NULL,
  applied_delay_days INT NOT NULL,
  is_overdue_vs_calc_date TINYINT(1) NOT NULL DEFAULT 0,
  shifted_for_weekend_or_holiday TINYINT(1) NOT NULL DEFAULT 0,
  raised_to_calculation_date TINYINT(1) NOT NULL DEFAULT 0,
  warning_text VARCHAR(1024) NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  updated_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  deleted_at DATETIME(3) NULL,
  PRIMARY KEY (id),
  KEY ix_sched_run (calculation_run_id),
  KEY ix_sched_run_forecast (calculation_run_id, forecast_payment_date),
  CONSTRAINT fk_sched_run FOREIGN KEY (calculation_run_id) REFERENCES payment_calculation_runs (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_sched_supplier FOREIGN KEY (supplier_id) REFERENCES suppliers (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_sched_contract FOREIGN KEY (contract_id) REFERENCES contracts (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_sched_debt FOREIGN KEY (incoming_debt_snapshot_id) REFERENCES incoming_debt_snapshots (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_sched_ship FOREIGN KEY (actual_shipment_id) REFERENCES actual_shipments (id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE payment_schedule_allocations (
  id BIGINT NOT NULL AUTO_INCREMENT,
  calculation_run_id BIGINT NOT NULL,
  schedule_item_id BIGINT NOT NULL,
  incoming_debt_snapshot_id BIGINT NOT NULL,
  actual_shipment_id BIGINT NOT NULL,
  allocated_amount DECIMAL(18,2) NOT NULL,
  debt_amount_before DECIMAL(18,2) NOT NULL,
  shipment_amount_total DECIMAL(18,2) NOT NULL,
  contract_condition_id BIGINT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY ix_alloc_item (schedule_item_id),
  CONSTRAINT fk_alloc_run FOREIGN KEY (calculation_run_id) REFERENCES payment_calculation_runs (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_alloc_item FOREIGN KEY (schedule_item_id) REFERENCES payment_schedule_items (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_alloc_debt FOREIGN KEY (incoming_debt_snapshot_id) REFERENCES incoming_debt_snapshots (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_alloc_ship FOREIGN KEY (actual_shipment_id) REFERENCES actual_shipments (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_alloc_cc FOREIGN KEY (contract_condition_id) REFERENCES contract_conditions (id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE uncovered_debt_items (
  id BIGINT NOT NULL AUTO_INCREMENT,
  calculation_run_id BIGINT NOT NULL,
  incoming_debt_snapshot_id BIGINT NOT NULL,
  uncovered_amount DECIMAL(18,2) NOT NULL,
  warning_text VARCHAR(1024) NOT NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY ix_unc_run (calculation_run_id),
  CONSTRAINT fk_unc_run FOREIGN KEY (calculation_run_id) REFERENCES payment_calculation_runs (id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_unc_debt FOREIGN KEY (incoming_debt_snapshot_id) REFERENCES incoming_debt_snapshots (id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE audit_log (
  id BIGINT NOT NULL AUTO_INCREMENT,
  occurred_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  user_id BIGINT NULL,
  action VARCHAR(64) NOT NULL,
  entity_type VARCHAR(64) NULL,
  entity_id VARCHAR(64) NULL,
  import_batch_id BIGINT NULL,
  calculation_run_id BIGINT NULL,
  ip_address VARCHAR(45) NULL,
  payload_json JSON NULL,
  created_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (id),
  KEY ix_audit_time (occurred_at),
  KEY ix_audit_user (user_id),
  CONSTRAINT fk_audit_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT fk_audit_batch FOREIGN KEY (import_batch_id) REFERENCES import_batches (id) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT fk_audit_run FOREIGN KEY (calculation_run_id) REFERENCES payment_calculation_runs (id) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
