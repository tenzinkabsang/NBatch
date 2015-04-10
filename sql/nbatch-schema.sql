
------- JOB TABLE -------
CREATE TABLE BatchJob(
	JobName varchar(100) NOT NULL, 
	CreateDate datetime NOT NULL, 
	LastRun datetime NOT NULL,
 CONSTRAINT [PK_BatchJob_1] PRIMARY KEY CLUSTERED 
(
	JobName ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

-------- STEP TABLE -------
CREATE TABLE BatchStep(
	Id bigint IDENTITY(1,1) NOT NULL,
	StepName nvarchar(100) NOT NULL,
	StepIndex bigint NOT NULL,
	NumberOfItemsProcessed int NOT NULL,
	JobName varchar(100) NOT NULL,
	LastRun datetime NOT NULL,
 CONSTRAINT [PK_BatchStep] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

ALTER TABLE [dbo].[BatchStep]  WITH CHECK ADD  CONSTRAINT [FK_BatchStep_BatchJob] FOREIGN KEY([JobName])
REFERENCES [dbo].[BatchJob] ([JobName])
GO

ALTER TABLE [dbo].[BatchStep] CHECK CONSTRAINT [FK_BatchStep_BatchJob]
GO

-------- STEP EXCEPTION TABLE --------
CREATE TABLE BatchStepException(
	Id bigint IDENTITY(1,1) NOT NULL,
	StepName varchar(100) NOT NULL,
	RowNumber int NOT NULL,
	ExceptionMsg varchar(500) NULL,
	ExceptionDetails varchar(1500) NULL,
	JobName varchar(100) NOT NULL,
	CreateDate datetime NOT NULL,
 CONSTRAINT [PK_BatchStepException] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

ALTER TABLE [dbo].[BatchStepException]  WITH CHECK ADD  CONSTRAINT [FK_BatchStepException_BatchJob] FOREIGN KEY([JobName])
REFERENCES [dbo].[BatchJob] ([JobName])
GO

ALTER TABLE [dbo].[BatchStepException] CHECK CONSTRAINT [FK_BatchStepException_BatchJob]
GO





