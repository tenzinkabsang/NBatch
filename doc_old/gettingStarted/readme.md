## Examples
- [Read from a file and write to a console](filereader-consolewriter.md)  
- [Read from a file and write to a database](filereader-sqlwriter.md)
- [Read from a file and write to a file](filereader-filewriter.md)
- [Read from a database and write to a console](sqlreader-consolewriter.md)
- [Read from a database and write to a file](sqlreader-filewriter.md)
- [Read from a database and write to a database](sqlreader-sqlwriter.md)

## Quick view of Job
![alt Job](../img/job.png)

## How Step works

![alt Sequence diagram of Step](../img/single-step.png)

## Getting Started
__NBatch__ can be installed via Nuget and consists of a single `.dll`. It should also create a folder named _nbatch-table-schemas_ containing a script file to create the necessary schemas for NBatch to work.


###### Job
A Job consists of steps and when executed will process each step sequentially. When creating a Job you must give it a unique name and a connection string name for the database where the above scripts were executed.    
```C#
	public Job(string jobName, string connectionStringName)
```   

###### Step
A Step has three main parts one of which is optional.  

- IReader<T\>: Used to read data from various sources (file, database, etc,.) and feeds it into the processor.  
	```C#
		public interface IReader<out TItem>
	```
	 

- IProcessor<T, U>: Handles any intermediary processes before sending it to a writer. Uses a default implementation if no custom implementation is provided.  
	```C#
		public interface IProcessor<in TInput, out TOutput>
	```  

- Writer<U\>: Used to write/save the items passed in through the processor.  
	```C#
		public interface IWriter<in TItem>
	```  

A Step also supports logic for skipping certain types of `ERRORS` as well as the `NUMBER` of times each errors should be skipped before throwing an exception. 



