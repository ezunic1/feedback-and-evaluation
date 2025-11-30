export type NewFeedbackEvent = {
  feedbackId: number;
  seasonId: number;
  senderUserId: string;
  receiverUserId: string;
  createdAtUtc: string;
};

export type DeleteRequestCreatedEvent = {
  deleteRequestId: number;
  feedbackId: number;
  senderUserId: string;
  reason: string;
  createdAtUtc: string;
};
