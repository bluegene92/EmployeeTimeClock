# TimeClock Admin — Features and Capabilities

Target list for **timeclock-admin**. Everything that was missing is now in scope. The **Status** column only says whether it already works.

**Assumption:** Employee clock-in and clock-out are entered by another application. Punch rows are already in the database when Admin opens this app. This admin app reviews and corrects that data, and manages employees and pay rates.

**Decided**

- Admin can add, edit, and delete a punch, including a punch the employee forgot.
- Pay uses one hourly rate per calendar day. Every hour that day uses that rate.
- Until the pay period is verified, a day Admin has not edited uses the employee’s current rate. If that rate changes, those days move to the new rate.
- A day Admin has edited keeps that edited rate, even if the employee rate changes before verify.
- Verify locks rates for that employee for the whole pay period. Clock times can still be changed. Unverify unlocks the rates.
- A forgotten punch on an unverified day starts at today’s employee rate. Admin can change that day’s rate on the grid. If the period is already verified, the new punch uses the rate already locked for that day.
- Salary is an employee pay type. Per-day rates apply to hourly employees only.

Status values:

| Status | Meaning |
|--------|---------|
| **Available** | Works in the admin UI today |
| **In scope** | Included in this app. Not built yet |

---

## Rate for the day

Hours are paid from the rate for that calendar day. An In and the matching Out do not have separate rates.

| Rule | Behavior |
|------|----------|
| Employee rate | Hourly employees have one current hourly rate. Salary employees have a salary amount instead. The salary is not copied onto days. |
| Day rate before verify | A day with no admin edit shows the employee’s current hourly rate. Changing the employee rate updates every unverified day that Admin has not edited. |
| Admin edit | On the bi-weekly grid, Admin can set a different rate for one day. That day keeps the edited rate. Other days are unchanged. |
| Forgotten punch | Adding a punch on a past day does not look up an old rate. The day uses today’s employee rate unless Admin then edits that day. |
| Verify | Admin verifies the whole employee for that pay period. Each day’s rate is frozen as it stands: current rate, or the admin edit. |
| After verify | A later employee rate change does not change those days. Clock times can still be added, edited, or deleted. Pay for a changed time uses the frozen day rate. |
| Unverify | Clears the lock so day rates follow the rules above again. Days Admin had edited stay on the edited rate. |
| Period wages | For hourly employees, sum of that day’s hours × that day’s rate. For salary employees, hours still show; pay is the salary on the employee, not hours × a day rate. |

A verified period does not rewrite itself when the employee’s rate changes. New days after that period, and any still-unverified period, use the new rate.

---

## Sign-in and shell

| Capability | What Admin can do | Status | Notes |
|------------|-------------------|--------|-------|
| Sign in | Enter username and password | Available | Demo account `admin` / `admin123`. Not a real user store. |
| Google sign-in | Click “Sign in with Google” | Available | Button only writes a fake session. No Google account is checked. |
| Sign out | End the browser session | Available | Clears `sessionStorage` and returns to the login page. |
| Session | Stay signed in for this browser tab session | Available | The API does not check this login. |
| Theme | Switch light and dark mode | Available | Preference is local to the browser. |
| Navigation | Open Employees, Time Clock, Reports, and Settings | Available | Sidebar can be collapsed. |

---

## Employees

| Capability | What Admin can do | Status | Notes |
|------------|-------------------|--------|-------|
| See employees | View the employee list | Available | Shows name, employee number, email, PIN status, hourly rate, and active/inactive. |
| Search | Search by name, email, or employee number | Available | Filters the list already loaded. |
| Filter by status | Show all, active only, or inactive only | Available | |
| Add employee | Create an employee | Available | Fields: first name, last name, email, employee number, active flag, optional hourly rate. |
| Open employee | Open one employee’s detail page | Available | Shows name, number, status, and email. |
| Edit name, email, or employee number | Change those fields after create | In scope | The API can update them. The detail page has no edit form yet. |
| Activate or deactivate one employee | Change status from the detail page | In scope | Today this is only possible in bulk, or at create time. |
| Bulk activate or deactivate | Mark selected employees active or inactive | Available | |
| Delete one employee | Permanently delete an employee | Available | Database also deletes that employee’s punches and wage rows. |
| Bulk delete | Delete every selected employee | Available | Same cascade as a single delete. |
| See clock PIN status | See whether a 4-digit PIN is set | Available | List shows “Set” or “—”. The PIN value is never shown after save. |
| Set, change, or clear PIN | Assign a new 4-digit clock PIN, or clear it | Available | Stored as a hash. Used by the other clock app. |
| Re-assign NFC card ID | Replace the card tied to an employee | In scope | No NFC field on the employee record yet. Tapping the card stays on the other application. |
| See hourly rate | See the current rate on the list and on the detail page | Available | This is the rate copied onto new, unverified punches. |
| Set rate on create | Enter an hourly rate when adding an employee | Available | Optional. |
| Change employee rate | Edit the current hourly rate | Available | Updates the rate used by unverified days Admin has not edited. Does not change a verified period, and does not change a day Admin already edited. |
| Salary | Mark an employee as salary and set the salary amount | In scope | Pay type on the employee. Hourly day rates stay for hourly employees only. The bi-weekly grid does not split the salary into a daily rate. |
| Wage history | See past employee rates and the date each rate started | In scope | A record of rate changes. Pay does not look up this history. Unverified days use the current rate. Verified days use the frozen day rate. |
| Photo | Upload an employee photo and keep it | In scope | Today the photo stays in this browser only. It should be stored with the employee. |

---

## Time

Normal punches come from the other application. Admin can still add, change, or delete a punch when someone forgot to clock in or out.

| Capability | What Admin can do | Status | Notes |
|------------|-------------------|--------|-------|
| See employee times | View In and Out times on a bi-weekly grid | Available | One row per active employee. Inactive employees with punches in the period are still hidden. |
| Show inactive employees who have time | Include them on the grid for that period | In scope | Their punches are in the database and need review. |
| Choose pay period | Pick a bi-weekly period | Available | About 16 periods around the current one. |
| Sort employees | Sort names A–Z or Z–A | Available | |
| Filter one employee | Show times for a single person | In scope | The API can filter by employee. The page always loads every active employee. |
| See daily hours | See hours for one day from In/Out pairs | Available | An In with no Out does not add hours. |
| See period hours | See total hours for the pay period | Available | Sum of daily paired hours. |
| See period wages | See pay from each day’s rate | In scope | Today the grid does hours × the employee’s current rate for the whole period. Target is each day’s hours × that day’s rate. Salary employees show hours, and pay is the salary on the employee. |
| Change a punch time | Edit the clock time on an existing punch | Available | Allowed before and after the period is verified. |
| Change punch type | Switch a punch between In and Out | Available | Same edit dialog. |
| Add a punch | Add an In or Out the employee forgot | Available | Manual correction. The day starts at today’s employee rate unless that day already has an admin rate edit or a verified rate. |
| Delete a punch | Remove a punch that should not be paid | Available | Allowed before and after the period is verified. |
| Edit the rate for a day | Change the rate for one day during bi-weekly review | In scope | Applies to every hour that day. Does not change the employee’s current rate or any other day. Locked after verify until the period is unverified. |
| Verify the pay period | Lock that employee’s day rates for the whole period | In scope | Times stay editable. No verified flag exists yet. |
| Unverify the pay period | Unlock that employee’s day rates for the period | In scope | Edited days keep the rate Admin set. Other days follow the current employee rate again. |
| See where a punch came from | See source app or device | In scope | The database stores `Source` and `DeviceId`. The grid does not show them. |
| Export times | Download the period for payroll | In scope | Hourly export uses each day’s rate, including admin edits and verified rates. |

---

## Reports and settings

| Capability | What Admin can do | Status | Notes |
|------------|-------------------|--------|-------|
| Hours report | Review hours worked outside the time grid | In scope | Reports page is a placeholder. |
| Overtime | See overtime hours or overtime pay | In scope | Overtime pay for an hourly employee uses that day’s rate. |
| Payroll export | Export a file for payroll | In scope | Same figures as the bi-weekly wage total. |
| Company settings | Edit company name or similar | In scope | Settings page is a placeholder. |
| Punch rules | Set rounding, grace periods, or required breaks | In scope | Rules apply when hours are calculated. They do not change a verified day’s locked rate. |

---

## Not part of this admin app

| Item | Owner |
|------|--------|
| Employee clocks in or out | Other application. Rows land in `TimePunches`. |
| PIN check at the clock | API endpoint used by the other application. |
| Employee list sync for the clock device | API endpoint used by the other application. |
| NFC tap at the clock | Other application. This admin app only re-assigns which card belongs to which employee. |

