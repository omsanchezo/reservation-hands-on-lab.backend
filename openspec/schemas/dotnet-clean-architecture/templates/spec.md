## ADDED Requirements

### Requirement: <!-- name -->
<!-- requirement description using SHALL/MUST -->

#### Scenario: <!-- scenario name -->
- **GIVEN** <!-- precondition (optional — omit if obvious) -->
- **WHEN** <!-- action or event -->
- **THEN** <!-- expected observable outcome -->

<!-- Example requirements:

### Requirement: Customer Data Model
The system SHALL manage customers with the following fields:
- name: required, max 200 characters
- rfc: required, unique, exactly 13 characters
- email: optional, valid email format
- phone: optional, max 15 characters

#### Scenario: Customer with all fields
- **WHEN** a customer is created with all fields populated
- **THEN** the system stores all fields and returns the complete customer record

### Requirement: Customer RFC Validation
The system SHALL enforce RFC format rules:
- RFC must be exactly 13 characters
- RFC must be unique across all customers

#### Scenario: Invalid RFC length
- **WHEN** a customer is created with an RFC shorter or longer than 13 characters
- **THEN** the system rejects the request with a validation error

#### Scenario: Duplicate RFC
- **GIVEN** a customer with RFC "XAXX010101000" already exists
- **WHEN** a new customer is created with the same RFC
- **THEN** the system returns a 409 Conflict error

### Requirement: Customer List Search
The system SHALL provide a searchable, paginated customer list.
- Text search across: name, RFC, email
- Search is case-insensitive and accent-insensitive
- Default page size: 25, maximum: 100

#### Scenario: Search by text
- **GIVEN** customers "Acme Corp" and "Beta Inc" exist
- **WHEN** a user searches with the text "acme"
- **THEN** the system returns only "Acme Corp"

#### Scenario: Paginated results
- **GIVEN** 50 customers exist
- **WHEN** a user requests page 2 with size 10
- **THEN** the system returns items 11-20 with total count of 50
-->

## MODIFIED Requirements

<!-- Copy ENTIRE existing requirement block, then edit. -->

## REMOVED Requirements

### Requirement: <!-- name -->
**Reason**: <!-- why removed -->
**Migration**: <!-- what to use instead -->

## RENAMED Requirements

### Requirement: <!-- new name -->
**FROM**: <!-- old name -->
**TO**: <!-- new name -->
