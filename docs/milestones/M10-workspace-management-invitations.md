# Milestone 10: Workspace Management And Invitations

## Status

In progress

## Objective

Support a professional multi-workspace collaboration model where users can
create workspaces, invite anyone by email, accept or decline invitations, and
only access workspace data where they are a member.

## User Stories

- As a new user, I get a default workspace where I am the owner.
- As a user, I can create additional workspaces and become their owner.
- As a workspace owner, I can invite a person by full name, email, and role.
- As an invited person, I can accept or decline an invitation from a secure
  link.
- As an invited person without an account, I can set my password during invite
  acceptance.
- As a workspace owner, I can view, resend, cancel, change, and remove members.
- As a removed member, I can no longer see dashboard, board, list, members,
  projects, or other workspace data.
- As a user in multiple workspaces, I can switch workspace and see cards,
  board, list, members, and projects update to that workspace.

## Technical Tasks

- Add workspace creation use case and endpoint.
- Add workspace invitation domain/persistence model.
- Add invitation create, list, accept, decline, resend, and cancel use cases.
- Add account creation during invitation acceptance for unknown emails.
- Add development email outbox abstraction for invite links.
- Add workspace switcher and selected-workspace persistence in React.
- Add member management UI with role change and removal.
- Add pending invitations UI with resend/cancel actions.
- Scope cards, board, list, members, projects, and task creation by selected
  workspace.

## Acceptance Criteria

- Users only receive workspaces where they are members.
- Creating a workspace creates owner membership and a starter project.
- Owners can invite unregistered or registered users by email.
- Invite tokens are single-use and expire.
- Accepting an invite adds the user to the workspace.
- Accepting an invite for an unknown email creates the account after password
  is set.
- Declined, cancelled, expired, and accepted invitations cannot be reused.
- Removed members cannot access workspace-scoped endpoints.
- Switching workspace changes dashboard, board, list, projects, and members.

## Required Tests

- Domain tests for invitation lifecycle rules.
- Application tests for workspace creation and invitation acceptance.
- Persistence tests for invitation token lookup and member isolation.
- API integration tests for create workspace, invite, accept, decline, role
  change, remove member, and forbidden access after removal.
- Frontend tests for workspace switching and member/invitation UI.

## Definition Of Done

- Multi-workspace behaviour is implemented end to end.
- Invitation flow works for both existing and new users.
- Security rules prevent workspace data leakage.
- UI exposes workspace switching and member/invite management.
- Tests and builds pass locally and in CI.
- Work is committed in multiple functional commits and pushed.

## Expected Commits

1. `docs: define workspace management invitation milestone`
2. `feat: add workspace creation use case`
3. `feat: model workspace invitations`
4. `feat: add invitation APIs`
5. `test: cover workspace invitation access rules`
6. `feat: add workspace switcher and member management UI`
7. `docs: complete workspace management invitation milestone`
