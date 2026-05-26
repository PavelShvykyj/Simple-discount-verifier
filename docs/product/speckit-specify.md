Build a web-based discount verification system for a restaurant business.

The system supports two separate business processes:

1. Administrator-managed customer eligibility registration.
2. Customer discount redemption at the time of payment.

The purpose of the system is to allow a restaurant to grant discounts only to pre-approved customers and to verify, at the moment of discount redemption, that the person requesting the discount has access to the phone number registered in the customer profile.

The system does not calculate the discount amount. The discount amount is calculated by the existing main restaurant business application based on the discount card found in that application.

Actors:

- Administrator: a restaurant employee or manager who is allowed to create customer profiles for customers who are approved to receive a discount.
- Customer: a person who wants to receive a discount at the restaurant.
- Main restaurant application: the existing business application used by the restaurant to process payments, scan discount cards, find discount cards, and calculate discounts.
- Discount verification web service: the new system that stores customer profiles, verifies phone ownership by SMS, issues one-time barcode codes, and validates those codes for the main restaurant application.

Business process 1: customer eligibility registration

An administrator signs in to a protected administrative web interface.

After successful authorization, the administrator creates a customer profile for a customer who has been approved to receive a discount.

The administrator fills in the customer profile data, including the customer phone number.

The phone number entered by the administrator is the primary identifier that will later be used to connect the customer profile with a discount card in the main restaurant application.

In the initial release, the phone number entered by the administrator is not verified by SMS during profile creation.

SMS verification during profile creation is out of scope for the initial release. It may be added in a later phase only to reduce the risk of administrator phone number entry mistakes.

A customer profile has no complex workflow state in the initial release. A profile either exists and is saved, or it does not exist.

The initial release must not include profile states such as Draft, PendingSync, Synced, or SyncError.

Business process 2: discount redemption

When the customer wants to receive a discount at the time of payment, the customer opens a public web page.

The customer manually enters their phone number into the public web page.

The system searches for an existing saved customer profile by the entered phone number.

If no saved customer profile exists for the entered phone number, the discount redemption process must stop and no SMS code or barcode must be issued.

If a saved customer profile exists for the entered phone number, the system sends an SMS verification code to that phone number.

The customer enters the SMS verification code into the public web page.

If the SMS code is invalid, expired, or the number of allowed attempts is exceeded, the discount redemption process must fail and no barcode must be issued.

If the SMS code is valid, the system immediately generates a random one-time discount code associated with the verified phone number.

The one-time discount code must be displayed to the customer as a barcode in the web application.

The barcode is shown to the restaurant cashier or operator and scanned by the main restaurant application.

The main restaurant application determines whether the scanned value is:

- a standard EAN13 discount card barcode, or
- a web-generated one-time discount code.

If the scanned value is a standard EAN13 discount card barcode, the main restaurant application must continue using its existing discount card process.

If the scanned value is a web-generated one-time discount code, the main restaurant application must call the discount verification web service to validate the code.

The discount verification web service validates the one-time code.

If the one-time code is invalid, expired, already used, or unknown, the service must return a failed validation result and the main restaurant application must not apply a discount based on that code.

If the one-time code is valid, the service must return the phone number or another agreed lookup key that allows the main restaurant application to find the corresponding discount card.

After successful validation, the one-time code must be immediately deleted or invalidated so it cannot be reused.

The main restaurant application then searches for the customer discount card by phone number.

If a matching discount card is found, the main restaurant application calculates and applies the discount.

If no matching discount card is found, the discount must not be applied, unless the main restaurant application supports a separate manual handling process.

The discount verification web service must not calculate the discount amount.

The discount verification web service only proves that the person presenting the barcode has successfully verified access to the phone number stored in a pre-approved customer profile.

One-time barcode rules

A one-time barcode code is created only after successful SMS verification during the discount redemption process.

The code must be random and hard to guess.

The code must be short-lived.

The default lifetime of the code should be 3 minutes and must be configurable.

Only one active one-time discount code should exist for the same phone number at the same time.

If the customer requests a new code while another active code exists for the same phone number, the system must either reject the request or invalidate the previous active code before issuing a new one.

The code must be usable only once.

The code must be deleted or invalidated immediately after successful validation by the main restaurant application.

A successfully validated code must not be restored even if the restaurant sale is later cancelled.

Sale cancellation after discount calculation is outside the responsibility of the discount verification web service and must be handled by the main restaurant application according to restaurant business rules.

Barcode format

Existing restaurant discount cards use EAN13 barcodes.

The new web-generated barcode should use a format distinguishable from EAN13, preferably Code 128, if supported by the scanners and the main restaurant application.

The generated barcode value should have a recognizable format or prefix that allows the main restaurant application to route it to the web-code validation flow instead of the standard EAN13 discount card flow.

The exact barcode format must be verified against real scanner behavior and the main restaurant application before final implementation.

Fraud prevention requirements

The system must minimize the risk of fraud by restaurant staff.

To reduce fraud risk:

- the customer must enter the phone number themselves in the public web page;
- the system must only continue if a customer profile exists for that phone number;
- the SMS code must be sent to the same phone number entered by the customer;
- the barcode must be issued only after successful SMS verification;
- the barcode must be short-lived;
- the barcode must be random and hard to guess;
- the barcode must be usable only once;
- the barcode must be invalidated immediately after successful validation;
- the web-generated barcode must be distinguishable from ordinary EAN13 discount cards;
- all important events must be logged for traceability and fraud investigation.

Logging and traceability

The system must provide traceability from the moment a customer requests SMS verification until the barcode is validated, rejected, expired, or invalidated.

Each discount redemption flow must have a correlation id.

The correlation id must be a random identifier and must not be the raw phone number.

The system should store a phone hash to support troubleshooting by phone number without exposing the raw phone number in logs.

The following business events should be logged:

- customer profile created;
- customer profile updated;
- phone verification requested;
- SMS send requested;
- SMS sent successfully;
- SMS send failed;
- SMS code validation failed;
- phone verified;
- one-time barcode issued;
- barcode validation requested by the main restaurant application;
- barcode validation failed;
- barcode validation succeeded;
- barcode consumed or invalidated;
- barcode expired.

Data lifecycle and cleanup

Temporary data must be cleaned up.

SMS verification codes must expire and must not remain usable after expiration.

One-time barcode codes must expire after a short lifetime, defaulting to 3 minutes.

Expired one-time codes must be removed from active code storage.

Consumed one-time codes must be removed from active code storage immediately after successful validation.

Audit events must not be deleted immediately, because they are needed for troubleshooting, support, and fraud investigation.

Out of scope for the initial release

The following are out of scope for the initial release:

- SMS verification of the phone number during administrator profile creation;
- complex customer profile workflow states;
- profile draft handling;
- profile synchronization states;
- discount calculation inside the discount verification web service;
- public API exposed by the main restaurant application;
- long-lived coupons;
- manual cancellation state for one-time codes;
- reservation/redeem lifecycle for barcode codes;
- restoring a barcode after sale cancellation;
- advanced administrator role model;
- detailed profile change history.

Future considerations

Future versions may add:

- SMS verification during customer profile creation to reduce phone number entry mistakes;
- detailed audit history for administrator actions;
- configurable administrator roles;
- stronger reporting for fraud investigation;
- improved barcode compatibility checks;
- additional validation rules for repeated SMS requests;
- additional anti-fraud limits per phone number, device, IP address, or restaurant location.
